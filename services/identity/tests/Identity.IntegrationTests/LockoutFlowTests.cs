using Identity.Application.Features.Registration;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>SignInHandler lockout and audit against a real Postgres, with a fake clock: AC-1, AC-2, AC-3, AC-6, AC-8.</summary>
public sealed class LockoutFlowTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac1_the_fifth_wrong_password_locks_for_fifteen_minutes()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("identity.invalid_credentials", (await Wrong(rig, email)).Failure!.Code);
        }

        var fifth = await Wrong(rig, email);

        Assert.Equal("identity.account_locked", fifth.Failure!.Code);
        await using var db = postgres.CreateContext();
        var lockout = await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
        Assert.Equal(5, lockout.FailedCount);
        Assert.Equal(clock.GetUtcNow().AddMinutes(15).ToUnixTimeMilliseconds(), lockout.LockedUntil!.Value.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Ac2_a_locked_account_rejects_the_correct_password_until_the_lock_ends_then_signs_in_and_clears_the_row()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await Wrong(rig, email);
        }

        var correctWhileLocked = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);
        clock.Advance(TimeSpan.FromMinutes(14));
        var stillLocked = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);
        clock.Advance(TimeSpan.FromMinutes(1));
        var afterTheLock = await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);

        Assert.Equal("identity.account_locked", correctWhileLocked.Failure!.Code);
        Assert.Equal("identity.account_locked", stillLocked.Failure!.Code);
        Assert.True(afterTheLock.IsSuccess, afterTheLock.Failure?.Code);
        await using var db = postgres.CreateContext();
        Assert.False(await db.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }

    [Fact]
    public async Task Ac3_failures_do_not_chain_across_windows_and_a_success_resets_the_count()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 4; i++)
        {
            await Wrong(rig, email);
        }

        clock.Advance(TimeSpan.FromMinutes(15));
        await Wrong(rig, email);

        await using (var db = postgres.CreateContext())
        {
            var lockout = await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
            Assert.Equal(1, lockout.FailedCount);
            Assert.Null(lockout.LockedUntil);
        }

        await Wrong(rig, email);
        await Wrong(rig, email);
        Assert.True((await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct)).IsSuccess);
        await using var after = postgres.CreateContext();
        Assert.False(await after.SignInLockouts.AnyAsync(l => l.UserId == userId, Ct));
    }

    [Fact]
    public async Task Ac6_an_unknown_email_never_creates_a_lockout_row()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var email = Rig.NewEmail();

        for (var i = 0; i < 7; i++)
        {
            Assert.Equal("identity.invalid_credentials", (await Wrong(rig, email)).Failure!.Code);
        }

        await using var db = postgres.CreateContext();
        Assert.Equal(7, await db.AuditEntries.CountAsync(a => a.UserId == null && a.Type == "sign_in_failed", Ct));
        Assert.Equal(0, await db.SignInLockouts.CountAsync(l => !db.Users.Any(u => u.Id == l.UserId), Ct));
    }

    [Fact]
    public async Task Ac6_an_account_without_a_credential_is_counted_and_locked_like_any_account()
    {
        await using var rig = new Rig(postgres, Rig.NewClock());
        var (userId, email, _) = await rig.RegisterAsync();
        await using (var db = postgres.CreateContext())
        {
            await db.Credentials.Where(c => c.UserId == userId).ExecuteDeleteAsync(Ct);
        }

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("identity.invalid_credentials", (await Wrong(rig, email)).Failure!.Code);
        }

        Assert.Equal("identity.account_locked", (await Wrong(rig, email)).Failure!.Code);
    }

    [Fact]
    public async Task Ac8_each_attempt_writes_one_audit_row_without_an_email_and_the_lock_adds_one()
    {
        var clock = Rig.NewClock();
        await using var rig = new Rig(postgres, clock);
        var (userId, email, _) = await rig.RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await Wrong(rig, email);
        }

        await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);
        clock.Advance(TimeSpan.FromMinutes(15));
        await rig.SignIn.HandleAsync(new SignInCommand(email, "hello1234", null), Ct);

        await using var db = postgres.CreateContext();
        var rows = await db.AuditEntries.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(Ct);
        Assert.Equal(5, rows.Count(r => r is { Type: "sign_in_failed", Reason: "identity.invalid_credentials" }));
        Assert.Equal(1, rows.Count(r => r.Type == "account_locked"));
        Assert.Equal(1, rows.Count(r => r is { Type: "sign_in_failed", Reason: "identity.account_locked" }));
        Assert.Equal(1, rows.Count(r => r.Type == "sign_in_succeeded"));
        Assert.Equal(8, rows.Count);
        Assert.All(rows, r => Assert.Equal(AuditMethod.Password, r.Method));
        Assert.DoesNotContain(rows, r => (r.Reason ?? string.Empty).Contains('@', StringComparison.Ordinal));
    }

    private static Task<HiveSpace.BuildingBlocks.Abstractions.Result<SignInOutcome>> Wrong(Rig rig, string email) =>
        rig.SignIn.HandleAsync(new SignInCommand(email, "wrong9999", null), Ct);
}
