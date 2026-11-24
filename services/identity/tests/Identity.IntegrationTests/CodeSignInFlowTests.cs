using System.Security.Cryptography;
using System.Text;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.CodeSignIn;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-006 email-code sign-in handlers against a real Postgres, with a fake clock.</summary>
public sealed class CodeSignInFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac1_an_account_gets_a_challenge_and_one_valid_event_and_only_hashes_are_stored()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var now = clock.GetUtcNow();

        var issued = await rig.IssueCodeAsync(email);

        Assert.Equal(now.AddMinutes(5), issued.ExpiresAt);
        Assert.Equal(now.AddSeconds(60), issued.ResendAvailableAt);
        await using var db = postgres.CreateContext();
        var events = await CodeEventsAsync(db, email);
        var data = Assert.Single(events);
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        Assert.True(schema.Evaluate(data).IsValid, "SignInCodeRequested data does not match its schema");
        var code = data.GetProperty("code").GetString()!;
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(userId, data.GetProperty("userId").GetGuid());

        var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
        Assert.Equal(userId, challenge.UserId);
        Assert.Equal(SHA256.HashData([.. issued.ChallengeId.ToByteArray(bigEndian: true), .. Encoding.UTF8.GetBytes(code)]), challenge.CodeHash);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(email)), challenge.EmailHash);
        var row = await RowTextAsync(db, issued.ChallengeId);
        Assert.DoesNotContain(code, row, StringComparison.Ordinal);
        Assert.DoesNotContain(email, row, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac2_an_unknown_address_gets_the_same_answer_a_challenge_without_user_and_no_event()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var email = Rig.NewEmail();
        var now = clock.GetUtcNow();

        var issued = await rig.IssueCodeAsync(email);

        Assert.Equal(now.AddMinutes(5), issued.ExpiresAt);
        Assert.Equal(now.AddSeconds(60), issued.ResendAvailableAt);
        await using var db = postgres.CreateContext();
        var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
        Assert.Null(challenge.UserId);
        Assert.False(challenge.IsEnded);
        Assert.Empty(await CodeEventsAsync(db, email));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ac3_a_second_request_within_sixty_seconds_is_too_soon_and_writes_nothing_then_works_at_sixty(bool registered)
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var email = registered ? (await rig.RegisterAsync()).Email : Rig.NewEmail();
        var first = await rig.IssueCodeAsync(email);

        clock.Advance(TimeSpan.FromSeconds(59));
        var tooSoon = await rig.RequestCode.HandleAsync(new RequestSignInCodeCommand(email), Ct);

        Assert.Equal(1, Assert.IsType<SignInCodeRequestOutcome.TooSoon>(tooSoon.Value).RetryAfterSeconds);
        await using (var db = postgres.CreateContext())
        {
            var rows = await ChallengesAsync(db, email);
            Assert.Equal([first.ChallengeId], rows.Select(c => c.Id));
            Assert.False(rows[0].IsEnded);
            Assert.Equal(registered ? 1 : 0, (await CodeEventsAsync(db, email)).Count);
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        var second = await rig.IssueCodeAsync(email);

        Assert.NotEqual(first.ChallengeId, second.ChallengeId);
    }

    [Fact]
    public async Task Ac3_parallel_requests_for_one_address_issue_exactly_one_challenge()
    {
        var clock = Rig.NewClock();
        var email = Rig.NewEmail();
        var rigs = Enumerable.Range(0, 8).Select(_ => new Rig(postgres, clock)).ToList();
        try
        {
            var results = await Task.WhenAll(rigs.Select(r => r.RequestCode.HandleAsync(new RequestSignInCodeCommand(email), Ct)));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Failure?.Code));
            Assert.Single(results, r => r.Value is SignInCodeRequestOutcome.Issued);
            Assert.All(
                results.Select(r => r.Value).OfType<SignInCodeRequestOutcome.TooSoon>(),
                t => Assert.InRange(t.RetryAfterSeconds, 1, 60));
            await using var db = postgres.CreateContext();
            Assert.Single(await ChallengesAsync(db, email));
        }
        finally
        {
            foreach (var rig in rigs)
            {
                await rig.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Ac4_a_new_code_supersedes_the_earlier_challenge()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (_, email, _) = await rig.RegisterAsync();
        var first = await rig.IssueCodeAsync(email);

        clock.Advance(TimeSpan.FromSeconds(61));
        var second = await rig.IssueCodeAsync(email);

        await using var db = postgres.CreateContext();
        var earlier = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == first.ChallengeId, Ct);
        var later = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == second.ChallengeId, Ct);
        Assert.Equal(ChallengeEndReason.Superseded, earlier.EndReason);
        Assert.Equal(clock.GetUtcNow().ToUnixTimeMilliseconds(), earlier.EndedAt!.Value.ToUnixTimeMilliseconds());
        Assert.False(later.IsEnded);
        Assert.Equal(2, (await CodeEventsAsync(db, email)).Count);
    }

    [Theory]
    [InlineData("a@")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Ac11_a_malformed_email_is_rejected_and_writes_nothing(string? email)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        await using var db = postgres.CreateContext();
        var before = await db.SignInCodeChallenges.CountAsync(Ct);

        var result = await rig.RequestCode.HandleAsync(new RequestSignInCodeCommand(email), Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        Assert.Contains(result.Failure!.Violations!, v => v.Field == "email");
        Assert.Equal(before, await db.SignInCodeChallenges.CountAsync(Ct));
    }

    [Fact]
    public async Task Ac2_a_challenge_of_an_unknown_address_answers_like_a_real_one()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (_, email, _) = await rig.RegisterAsync();
        var real = await rig.IssueCodeAsync(email);
        var realCode = await rig.CodeFromOutboxAsync((await UserIdAsync(email))!.Value);
        var unknown = await rig.IssueCodeAsync(Rig.NewEmail());
        var wrong = WrongCode(realCode!);

        var realAnswers = new List<string?>();
        var unknownAnswers = new List<string?>();
        for (var i = 0; i < 5; i++)
        {
            realAnswers.Add((await Submit(rig, real.ChallengeId, wrong)).Failure?.Code);
            unknownAnswers.Add((await Submit(rig, unknown.ChallengeId, wrong)).Failure?.Code);
        }

        string[] expected = [.. Enumerable.Repeat("identity.sign_in_code_invalid", 4), "identity.sign_in_challenge_ended"];
        Assert.Equal(expected, unknownAnswers);
        // The real account is locked by the fifth failure; up to there the answers are identical.
        Assert.Equal(expected.Take(4), realAnswers.Take(4));
        await using var db = postgres.CreateContext();
        var ended = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == unknown.ChallengeId, Ct);
        Assert.Equal(ChallengeEndReason.Attempts, ended.EndReason);
    }

    [Fact]
    public async Task Ac4_the_correct_code_of_a_superseded_challenge_is_ended()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var first = await rig.IssueCodeAsync(email);
        var firstCode = await rig.CodeFromOutboxAsync(userId);
        clock.Advance(TimeSpan.FromSeconds(60));
        await rig.IssueCodeAsync(email);

        var result = await Submit(rig, first.ChallengeId, firstCode!);

        Assert.Equal("identity.sign_in_challenge_ended", result.Failure?.Code);
    }

    [Fact]
    public async Task Ac5_the_correct_code_signs_in_once_and_ends_the_challenge_used()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);
        var code = await rig.CodeFromOutboxAsync(userId);
        clock.Advance(TimeSpan.FromMinutes(4));

        var first = await rig.SignInWithCode.HandleAsync(new SignInWithCodeCommand(issued.ChallengeId.ToString(), " " + code + " ", "/connect/authorize?client_id=x"), Ct);
        var again = await Submit(rig, issued.ChallengeId, code!);

        Assert.True(first.IsSuccess, first.Failure?.Code);
        Assert.Equal(userId, first.Value!.User.Id);
        Assert.Equal("/connect/authorize?client_id=x", first.Value.RedirectUrl);
        Assert.NotNull(first.Value.PasswordStamp);
        Assert.Equal(clock.GetUtcNow(), first.Value.AuthTime);
        Assert.Equal("identity.sign_in_challenge_ended", again.Failure?.Code);
        await using var db = postgres.CreateContext();
        var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
        Assert.Equal(ChallengeEndReason.Used, challenge.EndReason);
    }

    [Fact]
    public async Task Ac5_any_other_return_url_goes_home_and_an_account_without_a_password_has_no_stamp()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        await using (var db = postgres.CreateContext())
        {
            await db.Credentials.Where(c => c.UserId == userId).ExecuteDeleteAsync(Ct);
        }

        var issued = await rig.IssueCodeAsync(email);
        var code = await rig.CodeFromOutboxAsync(userId);

        var result = await rig.SignInWithCode.HandleAsync(new SignInWithCodeCommand(issued.ChallengeId.ToString(), code, "https://evil.example/connect/authorize"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal("/", result.Value!.RedirectUrl);
        Assert.Null(result.Value.PasswordStamp);
    }

    [Fact]
    public async Task Ac5_parallel_correct_submissions_sign_in_exactly_once()
    {
        var clock = Rig.NewClock();
        await using var setup = new Rig(postgres, clock);
        var (userId, email, _) = await setup.RegisterAsync();
        var issued = await setup.IssueCodeAsync(email);
        var code = await setup.CodeFromOutboxAsync(userId);
        var rigs = Enumerable.Range(0, 6).Select(_ => new Rig(postgres, clock)).ToList();
        try
        {
            var results = await Task.WhenAll(rigs.Select(r => Submit(r, issued.ChallengeId, code!)));

            Assert.Single(results, r => r.IsSuccess);
            Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Equal("identity.sign_in_challenge_ended", r.Failure?.Code));
            await using var db = postgres.CreateContext();
            Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.UserId == userId && a.Type == "sign_in_succeeded", Ct));
        }
        finally
        {
            foreach (var rig in rigs)
            {
                await rig.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Ac6_wrong_codes_count_on_the_challenge_and_the_lockout_and_the_fifth_ends_the_challenge()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);
        var code = (await rig.CodeFromOutboxAsync(userId))!;

        var first = await Submit(rig, issued.ChallengeId, WrongCode(code));

        Assert.Equal("identity.sign_in_code_invalid", first.Failure?.Code);
        await using (var db = postgres.CreateContext())
        {
            Assert.Equal(1, (await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct)).FailedCount);
            Assert.Equal(1, (await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct)).FailedCount);
        }

        // A success elsewhere resets the account count, so the challenge can reach five without locking the account.
        await rig.Recorder.RecordSuccessAsync(userId, AuditMethod.Password, Ct);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal("identity.sign_in_code_invalid", (await Submit(rig, issued.ChallengeId, WrongCode(code))).Failure?.Code);
        }

        var fifth = await Submit(rig, issued.ChallengeId, WrongCode(code));
        var correctAfter = await Submit(rig, issued.ChallengeId, code);

        Assert.Equal("identity.sign_in_challenge_ended", fifth.Failure?.Code);
        Assert.Equal("identity.sign_in_challenge_ended", correctAfter.Failure?.Code);
        await using var read = postgres.CreateContext();
        var challenge = await read.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
        Assert.Equal((5, ChallengeEndReason.Attempts), (challenge.FailedCount, challenge.EndReason));
        Assert.Equal(4, (await read.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct)).FailedCount);
    }

    [Fact]
    public async Task Ac7_an_expired_code_is_rejected_and_nothing_is_counted()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);
        var code = await rig.CodeFromOutboxAsync(userId);
        clock.Advance(TimeSpan.FromMinutes(5));

        var result = await Submit(rig, issued.ChallengeId, code!);
        var wrong = await Submit(rig, issued.ChallengeId, WrongCode(code!));

        Assert.Equal("identity.sign_in_code_expired", result.Failure?.Code);
        Assert.Equal("identity.sign_in_code_expired", wrong.Failure?.Code);
        await using var db = postgres.CreateContext();
        var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
        Assert.Equal(0, challenge.FailedCount);
        Assert.False(challenge.IsEnded);
        Assert.False(await db.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }

    [Fact]
    public async Task Ac8_password_and_code_failures_share_the_lockout_and_a_correct_code_clears_it()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);
        var code = (await rig.CodeFromOutboxAsync(userId))!;
        for (var i = 0; i < 3; i++)
        {
            await rig.SignIn.HandleAsync(new Identity.Application.Features.Registration.SignInCommand(email, "wrong9999", null), Ct);
        }

        var fourth = await Submit(rig, issued.ChallengeId, WrongCode(code));
        var fifth = await Submit(rig, issued.ChallengeId, WrongCode(code));
        var correctWhileLocked = await Submit(rig, issued.ChallengeId, code);

        Assert.Equal("identity.sign_in_code_invalid", fourth.Failure?.Code);
        Assert.Equal("identity.account_locked", fifth.Failure?.Code);
        Assert.Equal("identity.account_locked", correctWhileLocked.Failure?.Code);
        await using (var db = postgres.CreateContext())
        {
            Assert.Equal(2, (await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct)).FailedCount);
            Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.UserId == userId && a.Type == "account_locked" && a.Method == AuditMethod.Code, Ct));
        }

        // After the lock, a new code signs in and removes the lockout row.
        clock.Advance(TimeSpan.FromMinutes(15));
        var next = await rig.IssueCodeAsync(email);
        var signedIn = await Submit(rig, next.ChallengeId, (await rig.CodeFromOutboxAsync(userId))!);

        Assert.True(signedIn.IsSuccess, signedIn.Failure?.Code);
        await using var read = postgres.CreateContext();
        Assert.False(await read.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }

    [Fact]
    public async Task Ac8_ten_parallel_wrong_codes_count_five_on_both_and_lock_the_account()
    {
        var clock = Rig.NewClock();
        await using var setup = new Rig(postgres, clock);
        var (userId, email, _) = await setup.RegisterAsync();
        var issued = await setup.IssueCodeAsync(email);
        var wrong = WrongCode((await setup.CodeFromOutboxAsync(userId))!);
        var rigs = Enumerable.Range(0, 10).Select(_ => new Rig(postgres, clock)).ToList();
        try
        {
            var results = await Task.WhenAll(rigs.Select(r => Submit(r, issued.ChallengeId, wrong)));

            Assert.All(results, r => Assert.Contains(
                r.Failure?.Code,
                new[] { "identity.sign_in_code_invalid", "identity.sign_in_challenge_ended", "identity.account_locked" }));
            await using var db = postgres.CreateContext();
            var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct);
            var lockout = await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
            Assert.Equal((5, ChallengeEndReason.Attempts), (challenge.FailedCount, challenge.EndReason));
            Assert.Equal(5, lockout.FailedCount);
            Assert.NotNull(lockout.LockedUntil);
            Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.UserId == userId && a.Type == "account_locked", Ct));
        }
        finally
        {
            foreach (var rig in rigs)
            {
                await rig.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Ac9_a_code_sign_in_verifies_the_email()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);

        var result = await Submit(rig, issued.ChallengeId, (await rig.CodeFromOutboxAsync(userId))!);

        Assert.True(result.Value!.User.IsEmailVerified);
        await using var db = postgres.CreateContext();
        Assert.NotNull((await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, Ct)).EmailVerifiedAt);
    }

    [Fact]
    public async Task Ac10_every_submission_writes_one_code_audit_row_and_a_request_writes_none()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);
        var code = (await rig.CodeFromOutboxAsync(userId))!;
        await using (var db = postgres.CreateContext())
        {
            Assert.False(await db.AuditEntries.AnyAsync(a => a.UserId == userId, Ct));
        }

        // The clock moves between submissions so the rows read back in order.
        await Submit(rig, issued.ChallengeId, WrongCode(code));
        clock.Advance(TimeSpan.FromSeconds(1));
        await Submit(rig, issued.ChallengeId, code);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Submit(rig, issued.ChallengeId, code);
        var unknown = await rig.IssueCodeAsync(Rig.NewEmail());
        await Submit(rig, unknown.ChallengeId, "000000");

        await using var read = postgres.CreateContext();
        var rows = await read.AuditEntries.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.OccurredAt).ToListAsync(Ct);
        Assert.Equal(
            [("sign_in_failed", "identity.sign_in_code_invalid"), ("sign_in_succeeded", (string?)null), ("sign_in_failed", "identity.sign_in_challenge_ended")],
            rows.Select(r => (r.Type, r.Reason)));
        Assert.All(rows, r => Assert.Equal(AuditMethod.Code, r.Method));
        Assert.Contains(
            await read.AuditEntries.AsNoTracking().Where(a => a.UserId == null && a.Method == AuditMethod.Code).ToListAsync(Ct),
            a => a.Reason == "identity.sign_in_code_invalid");
        // The text columns of every code row: type, reason and correlation id hold no code and no email.
        var texts = (await read.AuditEntries.AsNoTracking().Where(a => a.Method == AuditMethod.Code).ToListAsync(Ct))
            .SelectMany(a => new[] { a.Type, a.Reason, a.CorrelationId })
            .OfType<string>()
            .ToList();
        Assert.DoesNotContain(texts, t => t.Contains(code, StringComparison.Ordinal) || t.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0197a000-0000-7000-8000-000000000000")]
    public async Task Ac11_an_unknown_or_malformed_challenge_id_is_ended(string? challengeId)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.SignInWithCode.HandleAsync(new SignInWithCodeCommand(challengeId, "123456", null), Ct);

        Assert.Equal("identity.sign_in_challenge_ended", result.Failure?.Code);
    }

    [Fact]
    public async Task Ac11_a_code_that_is_not_six_digits_writes_nothing_and_counts_nothing()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        var issued = await rig.IssueCodeAsync(email);

        var result = await Submit(rig, issued.ChallengeId, "12ab");

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        await using var db = postgres.CreateContext();
        Assert.Equal(0, (await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == issued.ChallengeId, Ct)).FailedCount);
        Assert.False(await db.AuditEntries.AnyAsync(a => a.UserId == userId, Ct));
        Assert.False(await db.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }

    private static Task<HiveSpace.BuildingBlocks.Abstractions.Result<Identity.Application.Features.Registration.SignInOutcome>> Submit(Rig rig, Guid challengeId, string code) =>
        rig.SignInWithCode.HandleAsync(new SignInWithCodeCommand(challengeId.ToString(), code, null), Ct);

    private static string WrongCode(string code) => code == "000000" ? "000001" : "000000";

    private async Task<Guid?> UserIdAsync(string email)
    {
        await using var db = postgres.CreateContext();
        return (await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.NormalizedEmail == email, Ct))?.Id;
    }

    private static async Task<List<SignInCodeChallenge>> ChallengesAsync(IdentityDbContext db, string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email));
        return await db.SignInCodeChallenges.AsNoTracking().Where(c => c.EmailHash == hash).OrderBy(c => c.CreatedAt).ToListAsync(Ct);
    }

    private static async Task<List<System.Text.Json.JsonElement>> CodeEventsAsync(IdentityDbContext db, string email) =>
        (await db.Set<OutboxMessage>().AsNoTracking().Where(m => m.EventType == "SignInCodeRequested").ToListAsync(Ct))
            .Select(m => EventEnvelope.FromJson(m.Envelope).Data)
            .Where(d => d.GetProperty("email").GetString() == email)
            .ToList();

    private static async Task<string> RowTextAsync(IdentityDbContext db, Guid id) =>
        await db.Database
            .SqlQuery<string>($"select row_to_json(c)::text as \"Value\" from sign_in_code_challenges c where c.id = {id}")
            .SingleAsync(Ct);

    private static string SchemaPath([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "SignInCodeRequested", "v1.schema.json");
    }
}
