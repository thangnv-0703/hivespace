using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Registration;

public sealed record SignInCommand(string? Email, string? Password, string? ReturnUrl);

/// <summary>The signed-in user and where the browser goes next.</summary>
public sealed record SignInOutcome(User User, string RedirectUrl);

/// <summary>
/// FR-AUTH-009, FR-AUTH-010: unknown email, wrong password and no password all fail the same way. An unverified email may
/// sign in. Lockout is FEAT-005.
/// </summary>
public sealed class SignInHandler(IUserRepository users, IPasswordHasher hasher)
{
    private const string AuthorizePath = "/connect/authorize";

    public async Task<Result<SignInOutcome>> HandleAsync(SignInCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(Email.Normalize(command.Email), cancellationToken).ConfigureAwait(false);
        var credential = user is null
            ? null
            : await users.FindCredentialAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // Always run a verification, against a dummy hash when there is no credential (timing, FR-AUTH-010).
        var valid = hasher.Verify(credential?.PasswordHash, command.Password ?? string.Empty);
        if (user is null || credential is null || !valid)
        {
            return Result<SignInOutcome>.Fail("identity.invalid_credentials");
        }

        return Result<SignInOutcome>.Ok(new SignInOutcome(user, SafeRedirect(command.ReturnUrl)));
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
