using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Registration;

/// <summary>FR-AUTH-005, FR-AUTH-006: looks the token up by its hash and verifies the email.</summary>
public sealed class VerifyEmailHandler(IUserRepository users, IVerificationTokenGenerator tokens, TimeProvider clock)
{
    public async Task<Result<Unit>> HandleAsync(string? presentedToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
        {
            return Result<Unit>.Fail("identity.verification_token_invalid");
        }

        var token = await users.FindTokenAsync(tokens.Hash(presentedToken.Trim()), cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return Result<Unit>.Fail("identity.verification_token_invalid");
        }

        var now = clock.GetUtcNow();
        switch (token.Check(now))
        {
            case TokenCheck.AlreadyUsed:
                return Result<Unit>.Fail("identity.email_already_verified");
            case TokenCheck.Expired:
                return Result<Unit>.Fail("identity.verification_token_expired");
        }

        var user = await users.FindByIdAsync(token.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<Unit>.Fail("identity.verification_token_invalid");
        }

        if (user.IsEmailVerified)
        {
            return Result<Unit>.Fail("identity.email_already_verified");
        }

        user.VerifyEmail(now);
        token.MarkUsed(now);
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<Unit>.Ok(default);
    }
}
