using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Password;

/// <param name="AuthTime">Unix seconds of the sign-in behind the access token (<c>auth_time</c>), or null.</param>
/// <param name="Methods">The token's <c>amr</c> values.</param>
public sealed record SetMyPasswordCommand(
    Guid UserId,
    string? CurrentAuthorizationId,
    long? AuthTime,
    IReadOnlyCollection<string> Methods,
    string? NewPassword);

/// <summary>
/// FR-AUTH-027, UC-026 1a: an account with no password sets one after a Google sign-in in the last 10 minutes (D-5). One transaction
/// creates the credential, signs out the other sessions and queues <c>PasswordChanged</c> (D-8). Returns the new password stamp.
/// </summary>
public sealed class SetMyPasswordHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ISessionRevoker sessions,
    IEventOutbox outbox,
    TimeProvider clock)
{
    public const string ReauthenticationRequired = "identity.reauthentication_required";
    public const string PasswordAlreadySet = "identity.password_already_set";

    public static readonly TimeSpan ReauthWindow = TimeSpan.FromMinutes(10);

    public async Task<Result<long>> HandleAsync(SetMyPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<long>.Fail(ErrorCodes.Unauthenticated);
        }

        if (await users.FindCredentialAsync(user.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result<long>.Fail(PasswordAlreadySet);
        }

        var now = clock.GetUtcNow();
        if (!command.Methods.Contains("google", StringComparer.Ordinal)
            || command.AuthTime is not { } authTime
            || now.ToUnixTimeSeconds() - authTime > ReauthWindow.TotalSeconds)
        {
            return Result<long>.Fail(ReauthenticationRequired);
        }

        var violations = PasswordPolicy.Evaluate(command.NewPassword, "newPassword");
        if (violations.Count > 0)
        {
            return Result<long>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var credential = Credential.Create(user.Id, hasher.Hash(command.NewPassword!), now);
        users.AddCredential(credential);
        await sessions.RevokeOtherSessionsAsync(user.Id, command.CurrentAuthorizationId, cancellationToken).ConfigureAwait(false);
        outbox.Enqueue(
            ChangeMyPasswordHandler.PasswordChangedEvent,
            1,
            user.Id.ToString(),
            new PasswordChangedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, now));
        try
        {
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (CredentialExistsException)
        {
            return Result<long>.Fail(PasswordAlreadySet);
        }

        return Result<long>.Ok(credential.Stamp);
    }
}
