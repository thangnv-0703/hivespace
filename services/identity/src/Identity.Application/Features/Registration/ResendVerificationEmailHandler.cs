using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Registration;

/// <summary>
/// FR-AUTH-007: a malformed email is rejected; any other address succeeds silently (no enumeration). An unverified
/// account has its pending tokens invalidated and gets a new one through <c>VerificationEmailRequested</c> (D-6).
/// </summary>
public sealed class ResendVerificationEmailHandler(
    IUserRepository users,
    IVerificationTokenGenerator tokens,
    IEventOutbox outbox,
    IClock clock)
{
    public const string VerificationEmailRequestedEvent = "VerificationEmailRequested";

    public async Task<Result<Unit>> HandleAsync(string? emailAddress, CancellationToken cancellationToken)
    {
        var (email, violation) = Email.Create(emailAddress);
        if (violation is not null)
        {
            return Result<Unit>.Fail("common.validation_failed", [violation]);
        }

        var user = await users.FindByEmailAsync(email!.Normalized, cancellationToken).ConfigureAwait(false);
        if (user is null || user.IsEmailVerified)
        {
            return Result<Unit>.Ok(default);
        }

        var now = clock.UtcNow;
        foreach (var pending in await users.FindPendingTokensAsync(user.Id, cancellationToken).ConfigureAwait(false))
        {
            pending.Invalidate(now);
        }

        var generated = tokens.Generate();
        var token = VerificationToken.Issue(user.Id, generated.Hash, now);
        users.AddToken(token);
        outbox.Enqueue(
            VerificationEmailRequestedEvent,
            1,
            user.Id.ToString(),
            new VerificationEmailData(user.Id, user.EmailAddress, user.DisplayName, user.Language, generated.Token, token.ExpiresAt));

        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<Unit>.Ok(default);
    }
}
