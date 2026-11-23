using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.ExternalLogin;
using Identity.Application.Features.Registration;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>ExternalSignInHandler against a real Postgres (FEAT-004 T3): AC-1..AC-6 handler part, AC-9 sign-in part, AC-11.</summary>
public sealed class ExternalSignInFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ExternalIdentity Google(string email, string? subject = null, bool verified = true, string? name = "An Nguyen") =>
        new("google", subject ?? Guid.NewGuid().ToString("N"), email, verified, name);

    [Fact]
    public async Task Ac1_a_new_verified_identity_creates_a_verified_buyer_with_no_password_one_link_and_one_event()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var email = Rig.NewEmail();
        var identity = Google(email);

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(identity, "en"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Null(result.Value!.PasswordStamp);
        var userId = result.Value!.User.Id;
        await using var db = postgres.CreateContext();
        var user = await db.Users.Include(u => u.Roles).AsNoTracking().SingleAsync(u => u.Id == userId, Ct);
        Assert.True(user.IsEmailVerified);
        Assert.Equal("An Nguyen", user.DisplayName);
        Assert.Equal("en", user.Language);
        Assert.Equal([Identity.Domain.Role.Buyer], user.Roles.Select(r => r.Role));
        Assert.False(await db.Credentials.AnyAsync(c => c.UserId == userId, Ct));
        Assert.False(await db.VerificationTokens.AnyAsync(t => t.UserId == userId, Ct));
        var link = await db.ExternalLogins.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
        Assert.Equal(("google", identity.Subject), (link.Provider, link.Subject));
        Assert.Empty(await EventsOfAsync("UserRegistered", userId));

        var data = Assert.Single(await EventsOfAsync("ExternalLoginLinked", userId));
        Assert.True(data.GetProperty("accountCreated").GetBoolean());
        Assert.False(data.GetProperty("passwordRemoved").GetBoolean());
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.Equal("google", data.GetProperty("provider").GetString());
        Assert.DoesNotContain(identity.Subject!, data.GetRawText(), StringComparison.Ordinal);
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct));
        Assert.True(schema.Evaluate(data).IsValid, "ExternalLoginLinked data does not match its schema");
    }

    [Theory]
    [InlineData("  ", "an.nguyen")]
    [InlineData(null, "an.nguyen")]
    public async Task Ac1_without_a_name_the_display_name_is_the_local_part_of_the_email(string? name, string expected)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var email = $"an.nguyen+{Guid.NewGuid():N}@example.com";

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(email, name: name), "vi"), Ct);

        Assert.StartsWith(expected, result.Value!.User.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac1_a_long_name_is_cut_to_80_characters()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(Rig.NewEmail(), name: "  " + new string('a', 100)), "vi"), Ct);

        Assert.Equal(new string('a', 80), result.Value!.User.DisplayName);
    }

    [Fact]
    public async Task Ac2_a_verified_account_is_linked_to_the_same_user_and_keeps_its_password()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, token) = await rig.RegisterAsync();
        Assert.True((await rig.Verify.HandleAsync(token, Ct)).IsSuccess);
        var session = await SeedSessionAsync(userId);

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(email.ToUpperInvariant()), "en"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal(userId, result.Value!.User.Id);
        await using var db = postgres.CreateContext();
        var credential = await db.Credentials.AsNoTracking().SingleAsync(c => c.UserId == userId, Ct);
        Assert.Equal(credential.Stamp, result.Value!.PasswordStamp);
        Assert.Equal(1, await db.ExternalLogins.CountAsync(l => l.UserId == userId, Ct));
        Assert.Equal(("valid", "valid"), await StatusesAsync(session));
        var data = Assert.Single(await EventsOfAsync("ExternalLoginLinked", userId));
        Assert.False(data.GetProperty("accountCreated").GetBoolean());
        Assert.False(data.GetProperty("passwordRemoved").GetBoolean());
        await using var signIn = new Rig(postgres, Rig.NewClock());
        Assert.True((await signIn.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Ac3_an_unverified_account_is_verified_loses_its_password_sessions_and_pending_tokens()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, token) = await rig.RegisterAsync();
        var session = await SeedSessionAsync(userId);

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(email), "vi"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal(userId, result.Value!.User.Id);
        Assert.Null(result.Value!.PasswordStamp);
        await using var db = postgres.CreateContext();
        Assert.True((await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, Ct)).IsEmailVerified);
        Assert.False(await db.Credentials.AnyAsync(c => c.UserId == userId, Ct));
        Assert.Equal(("revoked", "revoked"), await StatusesAsync(session));
        Assert.All(
            await db.VerificationTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(Ct),
            t => Assert.NotNull(t.InvalidatedAt));
        Assert.Equal("identity.verification_token_expired", (await rig.Verify.HandleAsync(token, Ct)).Failure?.Code);
        var data = Assert.Single(await EventsOfAsync("ExternalLoginLinked", userId));
        Assert.False(data.GetProperty("accountCreated").GetBoolean());
        Assert.True(data.GetProperty("passwordRemoved").GetBoolean());
        await using var signIn = new Rig(postgres, Rig.NewClock());
        Assert.Equal("identity.invalid_credentials", (await signIn.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct)).Failure?.Code);
    }

    [Fact]
    public async Task Ac4_a_known_identity_signs_in_with_no_write_even_after_its_email_changed()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var first = Google(Rig.NewEmail());
        var created = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(first, "vi"), Ct);
        var (otherId, otherEmail, _) = await rig.RegisterAsync();
        var eventsBefore = await CountEventsAsync();

        await using var again = new Rig(postgres, Rig.NewClock());
        var same = await again.ExternalSignIn.HandleAsync(new ExternalSignInCommand(first, "vi"), Ct);
        var moved = await again.ExternalSignIn.HandleAsync(new ExternalSignInCommand(first with { Email = otherEmail }, "vi"), Ct);

        Assert.Equal(created.Value!.User.Id, same.Value!.User.Id);
        Assert.Equal(created.Value!.User.Id, moved.Value!.User.Id);
        Assert.Equal(eventsBefore, await CountEventsAsync());
        await using var db = postgres.CreateContext();
        Assert.Equal(1, await db.ExternalLogins.CountAsync(l => l.Subject == first.Subject, Ct));
        Assert.False(await db.ExternalLogins.AnyAsync(l => l.UserId == otherId, Ct));
        Assert.True(await db.Credentials.AnyAsync(c => c.UserId == otherId, Ct));
    }

    [Theory]
    [InlineData(false, "an@example.com", "identity.external_email_unverified")]
    [InlineData(true, null, "identity.external_email_unverified")]
    [InlineData(true, "not-an-email", "identity.external_login_failed")]
    public async Task Ac5_an_unusable_email_fails_and_writes_nothing(bool verified, string? email, string code)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var eventsBefore = await CountEventsAsync();
        var identity = new ExternalIdentity("google", Guid.NewGuid().ToString("N"), email is null ? null : email.Replace("an@", $"{Guid.NewGuid():N}@", StringComparison.Ordinal), verified, "An");

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(identity, "vi"), Ct);

        Assert.Equal(code, result.Failure?.Code);
        await NothingWrittenAsync(identity.Subject!, eventsBefore);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Ac6_an_empty_subject_fails(string? subject)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var eventsBefore = await CountEventsAsync();

        var result = await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(new ExternalIdentity("google", subject, Rig.NewEmail(), true, "An"), "vi"), Ct);

        Assert.Equal("identity.external_login_failed", result.Failure?.Code);
        Assert.Equal(eventsBefore, await CountEventsAsync());
    }

    [Fact]
    public async Task Ac6_an_account_already_linked_to_another_subject_fails_and_writes_nothing()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var email = Rig.NewEmail();
        Assert.True((await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(email), "vi"), Ct)).IsSuccess);
        var eventsBefore = await CountEventsAsync();
        var second = Google(email);

        await using var other = new Rig(postgres, Rig.NewClock());
        var result = await other.ExternalSignIn.HandleAsync(new ExternalSignInCommand(second, "vi"), Ct);

        Assert.Equal("identity.external_login_failed", result.Failure?.Code);
        await NothingWrittenAsync(second.Subject!, eventsBefore);
    }

    [Fact]
    public async Task Ac11_two_concurrent_first_sign_ins_give_one_user_and_one_link()
    {
        var identity = Google(Rig.NewEmail());
        var rigs = Enumerable.Range(0, 4).Select(_ => new Rig(postgres, Rig.NewClock())).ToList();
        try
        {
            var results = await Task.WhenAll(rigs.Select(r => r.ExternalSignIn.HandleAsync(new ExternalSignInCommand(identity, "vi"), Ct)));

            Assert.All(results, r => Assert.True(r.IsSuccess, r.Failure?.Code));
            Assert.Single(results.Select(r => r.Value!.User.Id).Distinct());
            await using var db = postgres.CreateContext();
            Assert.Equal(1, await db.ExternalLogins.CountAsync(l => l.Subject == identity.Subject, Ct));
            Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == identity.Email!.ToLowerInvariant(), Ct));
            Assert.Single(await EventsOfAsync("ExternalLoginLinked", results[0].Value!.User.Id));
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
    public async Task Ac9_a_credential_less_account_gets_invalid_credentials_like_an_unknown_email()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var email = Rig.NewEmail();
        Assert.True((await rig.ExternalSignIn.HandleAsync(new ExternalSignInCommand(Google(email), "vi"), Ct)).IsSuccess);

        var known = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);
        var unknown = await rig.SignIn.HandleAsync(new SignInCommand(Rig.NewEmail(), "hello1234", null), Ct);

        Assert.Equal("identity.invalid_credentials", known.Failure?.Code);
        Assert.Equal(unknown.Failure?.Code, known.Failure?.Code);
        Assert.Equal(unknown.Failure?.Violations, known.Failure?.Violations);
    }

    private async Task NothingWrittenAsync(string subject, int eventsBefore)
    {
        await using var db = postgres.CreateContext();
        Assert.False(await db.ExternalLogins.AnyAsync(l => l.Subject == subject, Ct));
        Assert.Equal(eventsBefore, await CountEventsAsync());
    }

    private async Task<int> CountEventsAsync()
    {
        await using var db = postgres.CreateContext();
        return await db.Set<OutboxMessage>().CountAsync(Ct);
    }

    private sealed record Session(string AuthorizationId, string TokenId);

    private async Task<Session> SeedSessionAsync(Guid userId)
    {
        await using var db = postgres.CreateContext();
        var authorization = new OpenIddictEntityFrameworkCoreAuthorization
        {
            Id = Guid.NewGuid().ToString(),
            Subject = userId.ToString(),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
            CreationDate = DateTime.UtcNow,
        };
        var token = new OpenIddictEntityFrameworkCoreToken
        {
            Id = Guid.NewGuid().ToString(),
            Subject = userId.ToString(),
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
            Authorization = authorization,
            CreationDate = DateTime.UtcNow,
        };
        db.Add(authorization);
        db.Add(token);
        await db.SaveChangesAsync(Ct);
        return new Session(authorization.Id, token.Id);
    }

    private async Task<(string? Authorization, string? Token)> StatusesAsync(Session session)
    {
        await using var db = postgres.CreateContext();
        var a = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking().SingleAsync(x => x.Id == session.AuthorizationId, Ct);
        var t = await db.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().SingleAsync(x => x.Id == session.TokenId, Ct);
        return (a.Status, t.Status);
    }

    private async Task<List<System.Text.Json.JsonElement>> EventsOfAsync(string eventType, Guid userId)
    {
        await using var db = postgres.CreateContext();
        var rows = await db.Set<OutboxMessage>().Where(m => m.EventType == eventType).ToListAsync(Ct);
        return rows.Select(r => EventEnvelope.FromJson(r.Envelope).Data).Where(d => d.GetProperty("userId").GetGuid() == userId).ToList();
    }

    private static string SchemaPath([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "ExternalLoginLinked", "v1.schema.json");
    }
}
