using Identity.Application.Features.Password;
using Identity.Application.Features.Registration;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-005 T4: change password and the lockout (AC-5, FR-AUTH-025 note, UC-026 2a).</summary>
public sealed class ChangePasswordLockoutTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac5_wrong_current_passwords_count_toward_the_same_lockout_as_wrong_sign_ins()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 3; i++)
        {
            await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);
        }

        var fourth = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "wrong9999", "world5678"), Ct);
        var fifth = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "wrong9999", "world5678"), Ct);

        Assert.Equal("common.validation_failed", fourth.Failure!.Code);
        Assert.Equal("identity.account_locked", fifth.Failure!.Code);
        await using var db = postgres.CreateContext();
        var locked = await db.AuditEntries.AsNoTracking().SingleAsync(a => a.UserId == userId && a.Type == "account_locked", Ct);
        Assert.Equal(AuditMethod.PasswordChange, locked.Method);
    }

    [Fact]
    public async Task Ac5_a_locked_account_cannot_change_its_password_and_nothing_is_evaluated()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);
        }

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        Assert.Equal("identity.account_locked", result.Failure!.Code);
        await using var db = postgres.CreateContext();
        var credential = await db.Credentials.AsNoTracking().SingleAsync(c => c.UserId == userId, Ct);
        Assert.True(new Identity.Infrastructure.Security.Argon2idPasswordHasher().Verify(credential.PasswordHash, "hello1234"));
        Assert.Equal(0, (await db.Set<HiveSpace.BuildingBlocks.Messaging.OutboxMessage>().Where(m => m.EventType == "PasswordChanged").ToListAsync(Ct))
            .Count(m => HiveSpace.BuildingBlocks.Messaging.EventEnvelope.FromJson(m.Envelope).Data.GetProperty("userId").GetGuid() == userId));
    }

    [Fact]
    public async Task Ac5_a_correct_current_password_clears_earlier_failures_even_when_the_new_password_is_refused()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 4; i++)
        {
            await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);
        }

        var refused = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "abc"), Ct);

        Assert.Equal("common.validation_failed", refused.Failure!.Code);
        await using var db = postgres.CreateContext();
        Assert.False(await db.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
        // The next wrong password is the first of a new count, not the fifth.
        Assert.Equal("identity.invalid_credentials", (await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct)).Failure!.Code);
    }

    [Fact]
    public async Task A_successful_change_clears_the_lockout_row()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        await rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);

        var result = await rig.ChangePassword.HandleAsync(new ChangeMyPasswordCommand(userId, null, "hello1234", "world5678"), Ct);

        Assert.True(result.IsSuccess, result.Failure?.Code);
        await using var db = postgres.CreateContext();
        Assert.False(await db.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }
}
