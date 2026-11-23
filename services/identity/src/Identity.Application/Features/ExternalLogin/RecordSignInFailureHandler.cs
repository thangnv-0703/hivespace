using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.ExternalLogin;

/// <summary>
/// Audits a sign-in that failed before <see cref="ExternalSignInHandler"/> ran (the provider step, an unavailable provider): one
/// <c>sign_in_failed</c> row with the method and the error code, no user id (NFR-SEC-009, FEAT-005 §5.2).
/// </summary>
public sealed class RecordSignInFailureHandler(IAuditLog audit, IUserRepository users, TimeProvider clock)
{
    public async Task HandleAsync(AuditMethod method, string reason, CancellationToken cancellationToken)
    {
        audit.Add(AuditEntry.SignInFailed(null, method, reason, clock.GetUtcNow()));
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
