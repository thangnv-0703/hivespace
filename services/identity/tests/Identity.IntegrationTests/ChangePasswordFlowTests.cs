using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Features.Password;
using Identity.Application.Features.Registration;
using Identity.Infrastructure.Persistence;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>ChangeMyPasswordHandler against a real Postgres: AC-1, AC-2, AC-4, AC-5, AC-6 (Identity part), AC-8.</summary>
public sealed class ChangePasswordFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac1_a_success_writes_a_new_argon2id_hash_and_returns_the_new_stamp()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        var before = await CredentialAsync(userId);
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddMinutes(5);

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        var after = await CredentialAsync(userId);
        Assert.StartsWith("$argon2id$", after.PasswordHash, StringComparison.Ordinal);
        Assert.NotEqual(before.PasswordHash, after.PasswordHash);
        Assert.Equal(after.Stamp, result.Value);
        await using var signIn = new Rig(postgres, Rig.NewClock());
        Assert.True((await signIn.SignIn.HandleAsync(new SignInCommand(email, "world5678", null), Ct)).IsSuccess);
        Assert.False((await signIn.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Ac2_a_success_revokes_the_other_authorizations_and_their_tokens_but_not_the_current_one()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        var current = await SeedSessionAsync(userId);
        var other = await SeedSessionAsync(userId);
        var someoneElse = await SeedSessionAsync(Guid.CreateVersion7());

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, current.AuthorizationId, "hello1234", "world5678"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        Assert.Equal(("valid", "valid"), await StatusesAsync(current));
        Assert.Equal(("revoked", "revoked"), await StatusesAsync(other));
        Assert.Equal(("valid", "valid"), await StatusesAsync(someoneElse));
    }

    [Fact]
    public async Task A_null_keep_id_revokes_every_authorization_of_the_user()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        var first = await SeedSessionAsync(userId);
        var second = await SeedSessionAsync(userId);

        await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        Assert.Equal(("revoked", "revoked"), await StatusesAsync(first));
        Assert.Equal(("revoked", "revoked"), await StatusesAsync(second));
    }

    [Theory]
    [InlineData("wrong9999", "world5678", "identity.current_password_incorrect")]
    [InlineData("", "world5678", "identity.current_password_required")]
    [InlineData(null, "world5678", "identity.current_password_required")]
    [InlineData("hello1234", "abc", "identity.password_too_short")]
    public async Task Ac4_ac5_a_failure_writes_nothing(string? current, string next, string expectedCode)
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        var other = await SeedSessionAsync(userId);
        var before = await CredentialAsync(userId);

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, current, next), Ct);

        Assert.Equal("common.validation_failed", result.Failure?.Code);
        Assert.Contains(result.Failure!.Violations!, v => v.Code == expectedCode);
        var after = await CredentialAsync(userId);
        Assert.Equal((before.PasswordHash, before.UpdatedAt), (after.PasswordHash, after.UpdatedAt));
        Assert.Equal(("valid", "valid"), await StatusesAsync(other));
        Assert.Empty(await EventsOfAsync(userId));
    }

    [Fact]
    public async Task Ac5_a_wrong_current_password_and_a_weak_new_one_are_reported_together_on_their_fields()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "wrong9999", "abc"), Ct);

        Assert.Equal(
            [
                ("currentPassword", "identity.current_password_incorrect"),
                ("newPassword", "identity.password_too_short"),
                ("newPassword", "identity.password_needs_digit"),
            ],
            result.Failure!.Violations!.Select(v => (v.Field, v.Code)));
        Assert.Empty(await EventsOfAsync(userId));
    }

    [Fact]
    public async Task Ac6_a_success_writes_exactly_one_event_that_matches_the_schema_and_holds_no_secret()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();

        await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        var data = Assert.Single(await EventsOfAsync(userId));
        Assert.Equal((email, "An", "vi"), (data.GetProperty("email").GetString(), data.GetProperty("displayName").GetString(), data.GetProperty("language").GetString()));
        var json = data.GetRawText();
        Assert.DoesNotContain("world5678", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hello1234", json, StringComparison.Ordinal);
        Assert.DoesNotContain("argon2", json, StringComparison.Ordinal);
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct));
        Assert.True(schema.Evaluate(data).IsValid, "PasswordChanged data does not match its schema");
    }

    [Fact]
    public async Task Ac8_an_account_without_a_credential_gets_password_not_set_and_nothing_is_written()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, _, _) = await rig.RegisterAsync();
        await using (var db = postgres.CreateContext())
        {
            await db.Credentials.Where(c => c.UserId == userId).ExecuteDeleteAsync(Ct);
        }

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        Assert.Equal("identity.password_not_set", result.Failure?.Code);
        Assert.Empty(await EventsOfAsync(userId));
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

    private async Task<Identity.Domain.Credential> CredentialAsync(Guid userId)
    {
        await using var db = postgres.CreateContext();
        return await db.Credentials.AsNoTracking().SingleAsync(c => c.UserId == userId, Ct);
    }

    private async Task<List<System.Text.Json.JsonElement>> EventsOfAsync(Guid userId)
    {
        await using var db = postgres.CreateContext();
        var rows = await db.Set<OutboxMessage>().Where(m => m.EventType == "PasswordChanged").ToListAsync(Ct);
        return rows.Select(r => EventEnvelope.FromJson(r.Envelope).Data).Where(d => d.GetProperty("userId").GetGuid() == userId).ToList();
    }

    private static string SchemaPath([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "PasswordChanged", "v1.schema.json");
    }
}
