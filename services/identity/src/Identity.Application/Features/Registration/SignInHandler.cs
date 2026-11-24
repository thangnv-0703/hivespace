using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Application.Features.Lockout;
using Identity.Domain;

namespace Identity.Application.Features.Registration;

public sealed record SignInCommand(string? Email, string? Password, string? ReturnUrl);

/// <summary>The signed-in user, where the browser goes next, the password stamp (null without a password) and the moment of this interactive sign-in.</summary>
public sealed record SignInOutcome(User User, string RedirectUrl, long? PasswordStamp, DateTimeOffset AuthTime);

/// <summary>
/// FR-AUTH-009, FR-AUTH-010: unknown email, wrong password and no password all fail the same way. An unverified email may
/// sign in. FR-AUTH-011, FR-AUTH-012: a locked account is rejected without checking the password; the fifth wrong password locks
/// it. Every attempt is audited (NFR-SEC-009), in the same save as the lockout change.
/// </summary>
public sealed class SignInHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ILockoutStore lockouts,
    IAuditLog audit,
    LockoutRecorder recorder,
    TimeProvider clock)
{
    public const string InvalidCredentials = "identity.invalid_credentials";
    public const string AccountLocked = "identity.account_locked";

    private const string AuthorizePath = "/connect/authorize";

    public async Task<Result<SignInOutcome>> HandleAsync(SignInCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(Email.Normalize(command.Email), cancellationToken).ConfigureAwait(false);
        if (user is not null
            && await lockouts.FindAsync(user.Id, cancellationToken).ConfigureAwait(false) is { } existing
            && existing.IsLocked(clock.GetUtcNow()))
        {
            // FR-AUTH-012: the password is not even checked.
            audit.Add(AuditEntry.SignInFailed(user.Id, AuditMethod.Password, AccountLocked, clock.GetUtcNow()));
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<SignInOutcome>.Fail(AccountLocked);
        }

        var credential = user is null
            ? null
            : await users.FindCredentialAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // Always run a verification, against a dummy hash when there is no credential (timing, FR-AUTH-010).
        var valid = hasher.Verify(credential?.PasswordHash, command.Password ?? string.Empty);
        if (user is null)
        {
            audit.Add(AuditEntry.SignInFailed(null, AuditMethod.Password, InvalidCredentials, clock.GetUtcNow()));
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<SignInOutcome>.Fail(InvalidCredentials);
        }

        if (credential is null || !valid)
        {
            var locked = await recorder.RecordFailureAsync(user.Id, AuditMethod.Password, InvalidCredentials, cancellationToken).ConfigureAwait(false);
            return Result<SignInOutcome>.Fail(locked ? AccountLocked : InvalidCredentials);
        }

        await recorder.RecordSuccessAsync(user.Id, AuditMethod.Password, cancellationToken).ConfigureAwait(false);
        return Result<SignInOutcome>.Ok(new SignInOutcome(user, SafeRedirect(command.ReturnUrl), credential.Stamp, clock.GetUtcNow()));
    }

    /// <summary>Only a local path under <c>/connect/authorize</c> is honored; anything else goes home (AC-17).</summary>
    public static string SafeRedirect(string? returnUrl)
    {
        if (returnUrl is not null
            && returnUrl.StartsWith(AuthorizePath, StringComparison.Ordinal)
            && (returnUrl.Length == AuthorizePath.Length || returnUrl[AuthorizePath.Length] is '?' or '/'))
        {
            return returnUrl;
        }

        return "/";
    }
}
