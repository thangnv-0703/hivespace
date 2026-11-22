using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Password;

public sealed record ChangeMyPasswordCommand(Guid UserId, string? CurrentAuthorizationId, string? CurrentPassword, string? NewPassword);

/// <summary>Payload of <c>PasswordChanged/v1</c> (contracts/events). No password, hash or token.</summary>
public sealed record PasswordChangedData(Guid UserId, string Email, string DisplayName, string Language, DateTimeOffset ChangedAt);

/// <summary>
/// FR-AUTH-024, FR-AUTH-025, FR-AUTH-003: checks the current password and the new one's policy together, then writes the new
/// hash, revokes the other sessions and queues <c>PasswordChanged</c> in one transaction. Returns the new password stamp (D-4).
/// </summary>
public sealed class ChangeMyPasswordHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ISessionRevoker sessions,
    IEventOutbox outbox,
    TimeProvider clock)
{
    public const string PasswordChangedEvent = "PasswordChanged";

    public async Task<Result<long>> HandleAsync(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
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

        var violations = new List<Violation>();
        if (string.IsNullOrEmpty(command.CurrentPassword))
        {
            violations.Add(new Violation("currentPassword", "identity.current_password_required"));
        }
        else if (!hasher.Verify(credential.PasswordHash, command.CurrentPassword))
        {
            violations.Add(new Violation("currentPassword", "identity.current_password_incorrect"));
        }

        violations.AddRange(PasswordPolicy.Evaluate(command.NewPassword, "newPassword"));
        if (violations.Count > 0)
        {
            return Result<long>.Fail("common.validation_failed", violations);
        }

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
