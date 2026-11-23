using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Lockout;

/// <summary>
/// Counts password failures and clears them (FR-AUTH-011), shared by sign-in and change password. Failures of one account may arrive
/// in parallel: the lockout row has an optimistic concurrency token, and on a conflict the recorder reads it again and reapplies, so
/// every failure is counted (AC-4, design D-3). Every conflict means another failure committed, and after <see cref="LockoutPolicy.Threshold"/>
/// of them the account is locked and later failures change nothing, so <c>Threshold + 1</c> attempts always end (design says "3": too few
/// for ten parallel requests, see tasks.md T3 note). The audit rows are staged in the same save (NFR-SEC-009).
/// </summary>
public sealed class LockoutRecorder(ILockoutStore lockouts, IAuditLog audit, IUserRepository users, TimeProvider clock)
{
    private const int Attempts = LockoutPolicy.Threshold + 1;

    /// <summary>
    /// Records one failure. <paramref name="failureReason"/> is the error code for a <c>sign_in_failed</c> row, or null when the caller
    /// audits no plain failure (a wrong current password only audits the lock). Returns true when the account is locked after it.
    /// </summary>
    public async Task<bool> RecordFailureAsync(Guid userId, AuditMethod method, string? failureReason, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var now = clock.GetUtcNow();
            var lockout = await lockouts.FindAsync(userId, cancellationToken).ConfigureAwait(false);
            bool locked;
            if (lockout is null)
            {
                lockout = SignInLockout.Start(userId, now);
                lockouts.Add(lockout);
                locked = false;
            }
            else
            {
                locked = lockout.RecordFailure(now);
            }

            var reason = lockout.IsLocked(now) && !locked ? "identity.account_locked" : failureReason;
            if (reason is not null)
            {
                audit.Add(AuditEntry.SignInFailed(userId, method, reason, now));
            }

            if (locked)
            {
                audit.Add(AuditEntry.AccountLocked(userId, method, now));
            }

            try
            {
                await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return lockout.IsLocked(now);
            }
            catch (ConcurrencyConflictException) when (attempt < Attempts)
            {
                // Another failure committed first and the repository dropped what was staged: read it again.
            }
        }
    }

    /// <summary>Stages the removal of the account's lockout row, if there is one: the count is "consecutive" (AC-3).</summary>
    public async Task ClearAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await lockouts.FindAsync(userId, cancellationToken).ConfigureAwait(false) is { } lockout)
        {
            lockouts.Remove(lockout);
        }
    }

    /// <summary>A successful password sign-in: clears the lockout and writes <c>sign_in_succeeded</c> in one save.</summary>
    public async Task RecordSuccessAsync(Guid userId, AuditMethod method, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await ClearAsync(userId, cancellationToken).ConfigureAwait(false);
            audit.Add(AuditEntry.SignInSucceeded(userId, method, clock.GetUtcNow()));
            try
            {
                await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ConcurrencyConflictException) when (attempt < Attempts)
            {
                // A parallel failure changed the row we were removing: read it again.
            }
        }
    }
}
