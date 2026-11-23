using Identity.Domain;
using Xunit;

namespace Identity.UnitTests;

/// <summary>FEAT-005 T2: the lockout rules (FR-AUTH-011, AC-1 and AC-3 domain part) and the audit factories (NFR-SEC-009).</summary>
public sealed class LockoutDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Ac1_the_fifth_failure_locks_for_fifteen_minutes()
    {
        var lockout = SignInLockout.Start(UserId, Now);

        for (var i = 1; i <= 3; i++)
        {
            Assert.False(lockout.RecordFailure(Now.AddMinutes(i)));
        }

        Assert.Equal(4, lockout.FailedCount);
        Assert.False(lockout.IsLocked(Now.AddMinutes(4)));

        var fifth = Now.AddMinutes(5);
        Assert.True(lockout.RecordFailure(fifth));
        Assert.Equal(5, lockout.FailedCount);
        Assert.Equal(fifth.AddMinutes(15), lockout.LockedUntil);
        Assert.True(lockout.IsLocked(fifth));
    }

    [Fact]
    public void IsLocked_is_false_at_the_exact_end_of_the_lock()
    {
        var lockout = LockedAt(Now);
        var until = lockout.LockedUntil!.Value;

        Assert.True(lockout.IsLocked(until.AddTicks(-1)));
        Assert.False(lockout.IsLocked(until));
    }

    [Fact]
    public void A_failure_while_locked_changes_nothing()
    {
        var lockout = LockedAt(Now);
        var until = lockout.LockedUntil;

        Assert.False(lockout.RecordFailure(Now.AddMinutes(1)));

        Assert.Equal(5, lockout.FailedCount);
        Assert.Equal(until, lockout.LockedUntil);
    }

    [Fact]
    public void Ac3_a_failure_after_the_window_restarts_the_count_at_one()
    {
        var lockout = SignInLockout.Start(UserId, Now);
        for (var i = 1; i <= 3; i++)
        {
            lockout.RecordFailure(Now.AddMinutes(i));
        }

        var later = Now.AddMinutes(15);
        Assert.False(lockout.RecordFailure(later));

        Assert.Equal(1, lockout.FailedCount);
        Assert.Equal(later, lockout.WindowStartedAt);
        Assert.Null(lockout.LockedUntil);
    }

    [Fact]
    public void A_failure_just_inside_the_window_still_counts()
    {
        var lockout = SignInLockout.Start(UserId, Now);

        lockout.RecordFailure(Now.AddMinutes(15).AddTicks(-1));

        Assert.Equal(2, lockout.FailedCount);
    }

    [Fact]
    public void Ac3_after_a_finished_lock_the_next_failure_restarts_at_one()
    {
        var lockout = LockedAt(Now);

        Assert.False(lockout.RecordFailure(lockout.LockedUntil!.Value));

        Assert.Equal(1, lockout.FailedCount);
        Assert.Null(lockout.LockedUntil);
    }

    [Fact]
    public void Audit_factories_set_type_method_reason_and_user()
    {
        var failed = AuditEntry.SignInFailed(null, AuditMethod.Password, "identity.invalid_credentials", Now);
        var succeeded = AuditEntry.SignInSucceeded(UserId, AuditMethod.Google, Now);
        var locked = AuditEntry.AccountLocked(UserId, AuditMethod.PasswordChange, Now);

        Assert.Equal((AuditEntry.SignInFailedType, AuditMethod.Password, (Guid?)null, "identity.invalid_credentials"), (failed.Type, failed.Method, failed.UserId, failed.Reason));
        Assert.Equal((AuditEntry.SignInSucceededType, AuditMethod.Google, (Guid?)UserId, (string?)null), (succeeded.Type, succeeded.Method, succeeded.UserId, succeeded.Reason));
        Assert.Equal((AuditEntry.AccountLockedType, AuditMethod.PasswordChange), (locked.Type, locked.Method));
        Assert.All(new[] { failed, succeeded, locked }, a => Assert.Equal(Now, a.OccurredAt));
        Assert.Equal(3, new[] { failed.Id, succeeded.Id, locked.Id }.Distinct().Count());
    }

    private static SignInLockout LockedAt(DateTimeOffset now)
    {
        var lockout = SignInLockout.Start(UserId, now);
        for (var i = 1; i < LockoutPolicy.Threshold; i++)
        {
            lockout.RecordFailure(now);
        }

        return lockout;
    }
}
