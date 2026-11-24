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
