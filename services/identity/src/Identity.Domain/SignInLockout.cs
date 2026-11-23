namespace Identity.Domain;

/// <summary>
/// Consecutive failed password attempts for one account (FR-AUTH-011). The row exists only while there are recent failures:
/// a success, or a correct current password, removes it. Concurrency is optimistic (design D-3).
/// </summary>
public sealed class SignInLockout
{
    private SignInLockout()
    {
    }

    public Guid UserId { get; private set; }

    public int FailedCount { get; private set; }

    public DateTimeOffset WindowStartedAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public static SignInLockout Start(Guid userId, DateTimeOffset now) =>
        new() { UserId = userId, FailedCount = 1, WindowStartedAt = now };

    public bool IsLocked(DateTimeOffset now) => LockedUntil > now;

    /// <summary>Counts one failure. Returns true when this call locked the account.</summary>
    public bool RecordFailure(DateTimeOffset now)
    {
        if (IsLocked(now))
        {
            return false;
        }

        var lockEnded = LockedUntil is not null;
        if (lockEnded || WindowStartedAt + LockoutPolicy.Window <= now)
        {
            FailedCount = 1;
            WindowStartedAt = now;
            LockedUntil = null;
            return false;
        }

        FailedCount++;
        if (FailedCount < LockoutPolicy.Threshold)
        {
            return false;
        }

        LockedUntil = now + LockoutPolicy.Duration;
        return true;
    }
}
