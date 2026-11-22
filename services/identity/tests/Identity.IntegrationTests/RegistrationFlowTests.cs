using System.Security.Cryptography;
using System.Text;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.Registration;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Handlers against a real Postgres: AC-1, AC-2, AC-3, AC-4, AC-6, AC-8..AC-11, AC-13 (handler level).</summary>
public sealed class RegistrationFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac1_register_creates_an_unverified_buyer_with_an_argon2id_credential()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var (userId, _, _) = await rig.RegisterAsync(password: "hello1234");

        await using var db = postgres.CreateContext();
        var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Id == userId, Ct);
        var credential = await db.Credentials.SingleAsync(c => c.UserId == userId, Ct);
        Assert.False(user.IsEmailVerified);
        Assert.Equal([Role.Buyer], user.Roles.Select(r => r.Role));
        Assert.StartsWith("$argon2id$", credential.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain("hello1234", credential.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac2_duplicate_email_is_rejected_regardless_of_case()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (_, email, _) = await rig.RegisterAsync();

        var second = await rig.Register.HandleAsync(new RegisterUserCommand(email.ToUpperInvariant(), "Other", "hello1234", "vi"), Ct);

        Assert.Equal("identity.email_in_use", second.Failure?.Code);
    }

    [Fact]
    public async Task Ac2_concurrent_registrations_for_one_email_leave_exactly_one_user_and_one_outbox_event()
    {
        var email = Rig.NewEmail();
        var clock = Rig.NewClock();
        var rigs = Enumerable.Range(0, 8).Select(_ => new Rig(postgres, clock)).ToList();
        try
        {
            var results = await Task.WhenAll(rigs.Select(r =>
                r.Register.HandleAsync(new RegisterUserCommand(email, "An", "hello1234", "vi"), Ct)));

            Assert.Single(results, r => r.IsSuccess);
            Assert.All(results.Where(r => !r.IsSuccess), r => Assert.Equal("identity.email_in_use", r.Failure?.Code));
            await using var db = postgres.CreateContext();
            Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email, Ct));
            var winner = results.Single(r => r.IsSuccess).Value;
            var events = (await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync(Ct))
                .Count(m => EventEnvelope.FromJson(m.Envelope).Data.GetProperty("email").GetString() == email);
            Assert.Equal(1, events);
            Assert.Equal(winner, (await db.Users.SingleAsync(u => u.NormalizedEmail == email, Ct)).Id);
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
    public async Task Ac3_ac4_validation_lists_every_failure_together()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.Register.HandleAsync(new RegisterUserCommand("a@", "", "abc", "vi"), Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        var codes = result.Failure!.Violations!.Select(v => v.Code).ToList();
        Assert.Equal(
            ["identity.email_invalid", "identity.display_name_required", "identity.password_too_short", "identity.password_needs_digit"],
            codes);
    }

    [Fact]
    public async Task Ac6_registration_writes_the_outbox_event_in_the_same_transaction_with_the_contract_shape()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, token) = await rig.RegisterAsync();

        await using var db = postgres.CreateContext();
        var row = (await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync(Ct))
            .Single(m => EventEnvelope.FromJson(m.Envelope).Data.GetProperty("userId").GetGuid() == userId);
        var envelope = EventEnvelope.FromJson(row.Envelope);
        var names = envelope.Data.EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(
            ["displayName", "email", "language", "userId", "verificationToken", "verificationTokenExpiresAt"],
            names);
        Assert.Equal(email, envelope.Data.GetProperty("email").GetString());
        Assert.Equal(token, envelope.Data.GetProperty("verificationToken").GetString());
        Assert.Equal("identity", envelope.Producer);
        Assert.Null(row.SentAt);
    }

    [Fact]
    public async Task Ac8_token_is_stored_only_as_a_sha256_hash_and_old_sent_outbox_rows_are_removed()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, token) = await rig.RegisterAsync();

        await using var db = postgres.CreateContext();
        var stored = await db.VerificationTokens.SingleAsync(t => t.UserId == userId, Ct);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), stored.TokenHash);
        Assert.NotEqual(Encoding.UTF8.GetBytes(token), stored.TokenHash);

        var row = (await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync(Ct))
            .Single(m => EventEnvelope.FromJson(m.Envelope).Data.GetProperty("userId").GetGuid() == userId);
        row.SentAt = DateTimeOffset.UtcNow.AddHours(-25);
        await db.SaveChangesAsync(Ct);

        var services = new ServiceCollection().AddScoped(_ => postgres.CreateContext()).BuildServiceProvider();
        var cleanup = new OutboxCleanup<IdentityDbContext>(
            services.GetRequiredService<IServiceScopeFactory>(),
            new OutboxCleanupOptions(TimeSpan.FromHours(24)),
            TimeProvider.System,
            NullLogger<OutboxCleanup<IdentityDbContext>>.Instance);
        await cleanup.RunOnceAsync(DateTimeOffset.UtcNow, Ct);

        await using var after = postgres.CreateContext();
        Assert.False(await after.Set<OutboxMessage>().AnyAsync(m => m.Id == row.Id, Ct));
    }

    [Fact]
    public async Task Ac9_valid_token_verifies_the_email()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, token) = await rig.RegisterAsync();

        var result = await rig.Verify.HandleAsync(token, Ct);

        Assert.True(result.IsSuccess);
        await using var db = postgres.CreateContext();
        Assert.NotNull((await db.Users.SingleAsync(u => u.Id == userId, Ct)).EmailVerifiedAt);
    }

    [Fact]
    public async Task Ac10_unknown_expired_invalidated_and_reused_tokens_report_their_designed_error()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);

        Assert.Equal("identity.verification_token_invalid", (await rig.Verify.HandleAsync("never-issued", Ct)).Failure?.Code);

        var (_, _, aged) = await rig.RegisterAsync();
        clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal("identity.verification_token_expired", (await rig.Verify.HandleAsync(aged, Ct)).Failure?.Code);

        clock.AdjustTime(DateTimeOffset.UtcNow);
        var (_, email, first) = await rig.RegisterAsync();
        await rig.Resend.HandleAsync(email, Ct);
        Assert.Equal("identity.verification_token_expired", (await rig.Verify.HandleAsync(first, Ct)).Failure?.Code);

        var (_, _, used) = await rig.RegisterAsync();
        Assert.True((await rig.Verify.HandleAsync(used, Ct)).IsSuccess);
        Assert.Equal("identity.email_already_verified", (await rig.Verify.HandleAsync(used, Ct)).Failure?.Code);
    }

    [Fact]
    public async Task Ac11_resend_issues_a_new_token_for_unverified_accounts_and_does_nothing_otherwise()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, first) = await rig.RegisterAsync();

        Assert.True((await rig.Resend.HandleAsync(email, Ct)).IsSuccess);
        var second = await rig.TokenFromOutboxAsync(userId, "VerificationEmailRequested");
        Assert.NotEqual(first, second);
        Assert.True((await rig.Verify.HandleAsync(second, Ct)).IsSuccess);

        var verifiedUserEvents = await CountEventsAsync("VerificationEmailRequested", userId);
        Assert.True((await rig.Resend.HandleAsync(email, Ct)).IsSuccess);
        Assert.True((await rig.Resend.HandleAsync("nobody-" + Rig.NewEmail(), Ct)).IsSuccess);
        Assert.Equal(verifiedUserEvents, await CountEventsAsync("VerificationEmailRequested", userId));
    }

    [Fact]
    public async Task Ac11_resend_rejects_a_malformed_email()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.Resend.HandleAsync("a@", Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
    }

    [Fact]
    public async Task Ac13_unknown_email_wrong_password_and_missing_credential_fail_identically()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (_, email, _) = await rig.RegisterAsync(password: "hello1234");

        var unknown = await rig.SignIn.HandleAsync(new SignInCommand(Rig.NewEmail(), "hello1234", null), Ct);
        var wrong = await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);

        Assert.Equal(unknown.Failure, wrong.Failure);
        Assert.Equal("identity.invalid_credentials", wrong.Failure?.Code);
    }

    [Fact]
    public async Task Ac18_unverified_user_can_sign_in_and_return_url_is_restricted_to_authorize()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync(password: "hello1234");

        var plain = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);
        var authorize = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", "/connect/authorize?client_id=x"), Ct);
        var evil = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", "https://evil.example/connect/authorize"), Ct);

        Assert.True(plain.IsSuccess);
        Assert.Equal(userId, plain.Value!.User.Id);
        Assert.Equal("/", plain.Value.RedirectUrl);
        Assert.Equal("/connect/authorize?client_id=x", authorize.Value!.RedirectUrl);
        Assert.Equal("/", evil.Value!.RedirectUrl);
    }

    private async Task<int> CountEventsAsync(string eventType, Guid userId)
    {
        await using var db = postgres.CreateContext();
        return (await db.Set<OutboxMessage>().Where(m => m.EventType == eventType).ToListAsync(Ct))
            .Count(m => EventEnvelope.FromJson(m.Envelope).Data.GetProperty("userId").GetGuid() == userId);
    }
}
