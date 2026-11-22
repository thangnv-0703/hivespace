using Identity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Revokes through OpenIddict's own tables on the shared <see cref="IdentityDbContext"/>, so the change commits atomically with the
/// credential and the outbox row (D-2). The manager API saves per call, which would not.
/// </summary>
internal sealed class OpenIddictSessionRevoker(IdentityDbContext db) : ISessionRevoker
{
    public async Task RevokeOtherSessionsAsync(Guid userId, string? keepAuthorizationId, CancellationToken cancellationToken)
    {
        var subject = userId.ToString();

        var authorizations = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .Where(a => a.Subject == subject && a.Status == OpenIddictConstants.Statuses.Valid && a.Id != keepAuthorizationId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (authorizations.Count == 0)
        {
            return;
        }

        var ids = authorizations.Select(a => a.Id).ToList();
        var tokens = await db.Set<OpenIddictEntityFrameworkCoreToken>()
            .Where(t => t.Authorization != null && ids.Contains(t.Authorization.Id!)
                && (t.Status == OpenIddictConstants.Statuses.Valid || t.Status == OpenIddictConstants.Statuses.Inactive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var authorization in authorizations)
        {
            authorization.Status = OpenIddictConstants.Statuses.Revoked;
        }

        foreach (var token in tokens)
        {
            token.Status = OpenIddictConstants.Statuses.Revoked;
        }
    }
}
