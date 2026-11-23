using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Application.Features.Lockout;
using Identity.Application.Features.Registration;
using Identity.Domain;

namespace Identity.Application.Features.Password;

public sealed record ChangeMyPasswordCommand(Guid UserId, string? CurrentAuthorizationId, string? CurrentPassword, string? NewPassword);

/// <summary>Payload of <c>PasswordChanged/v1</c> (contracts/events). No password, hash or token.</summary>
public sealed record PasswordChangedData(Guid UserId, string Email, string DisplayName, string Language, DateTimeOffset ChangedAt);

/// <summary>
/// FR-AUTH-024, FR-AUTH-025, FR-AUTH-003: checks the current password and the new one's policy together, then writes the new
/// hash, revokes the other sessions and queues <c>PasswordChanged</c> in one transaction. Returns the new password stamp (D-4).
/// FEAT-005: a locked account is rejected before anything is evaluated; a wrong current password counts toward the same lockout as
/// a wrong sign-in (FR-AUTH-025 note); a correct one clears it (UC-026 2a).
/// </summary>
public sealed class ChangeMyPasswordHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ISessionRevoker sessions,
    IEventOutbox outbox,
    ILockoutStore lockouts,
    LockoutRecorder recorder,
    TimeProvider clock)
{
    public const string PasswordChangedEvent = "PasswordChanged";

    private const int Attempts = 3;

    public async Task<Result<long>> HandleAsync(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await HandleOnceAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException) when (attempt < Attempts)
            {
                // A parallel failed sign-in changed the lockout row this change was clearing: decide again on fresh reads.
            }
        }
    }

    private async Task<Result<long>> HandleOnceAsync(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<long>.Fail("common.unauthenticated");
        }

        var credential = await users.FindCredentialAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return Result<long>.Fail("identity.password_not_set");
        }

        if (await lockouts.FindAsync(user.Id, cancellationToken).ConfigureAwait(false) is { } existing && existing.IsLocked(clock.GetUtcNow()))
        {
            return Result<long>.Fail(SignInHandler.AccountLocked);
        }

        var violations = new List<Violation>();
        var currentIsCorrect = false;
        if (string.IsNullOrEmpty(command.CurrentPassword))
        {
            violations.Add(new Violation("currentPassword", "identity.current_password_required"));
        }
        else if (!hasher.Verify(credential.PasswordHash, command.CurrentPassword))
        {
            violations.Add(new Violation("currentPassword", "identity.current_password_incorrect"));
            if (await recorder.RecordFailureAsync(user.Id, AuditMethod.PasswordChange, null, cancellationToken).ConfigureAwait(false))
            {
                return Result<long>.Fail(SignInHandler.AccountLocked);
            }
        }
        else
        {
            currentIsCorrect = true;
        }

        violations.AddRange(PasswordPolicy.Evaluate(command.NewPassword, "newPassword"));
        if (violations.Count > 0)
        {
            if (currentIsCorrect)
            {
                // A correct current password proves the owner: earlier failures stop counting even when the new password is refused.
                await recorder.ClearAsync(user.Id, cancellationToken).ConfigureAwait(false);
                await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Result<long>.Fail("common.validation_failed", violations);
        }

        await recorder.ClearAsync(user.Id, cancellationToken).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        credential.ChangePassword(hasher.Hash(command.NewPassword!), now);
        await sessions.RevokeOtherSessionsAsync(user.Id, command.CurrentAuthorizationId, cancellationToken).ConfigureAwait(false);
        outbox.Enqueue(
            PasswordChangedEvent,
            1,
            user.Id.ToString(),
            new PasswordChangedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, now));
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<long>.Ok(credential.Stamp);
    }
}
