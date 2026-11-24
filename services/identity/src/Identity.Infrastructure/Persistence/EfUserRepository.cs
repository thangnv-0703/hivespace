using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository(IdentityDbContext db, ILogger<EfUserRepository>? logger = null) : IUserRepository, ILockoutStore, IAuditLog, ISignInChallengeStore, IShopStore
{

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<Credential?> FindCredentialAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Credentials.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<VerificationToken?> FindTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        db.VerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<VerificationToken>> FindPendingTokensAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.VerificationTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.InvalidatedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(User user, Credential credential, VerificationToken token)
    {
        db.Users.Add(user);
        db.Credentials.Add(credential);
        db.VerificationTokens.Add(token);
    }

    public void AddToken(VerificationToken token) => db.VerificationTokens.Add(token);

    public void AddCredential(Credential credential) => db.Credentials.Add(credential);

    public void Add(User user) => db.Users.Add(user);

    public Task<ExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken) =>
        db.ExternalLogins.FirstOrDefaultAsync(l => l.Provider == provider && l.Subject == subject, cancellationToken);

    public Task<ExternalLogin?> FindExternalLoginByUserAsync(Guid userId, string provider, CancellationToken cancellationToken) =>
        db.ExternalLogins.FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider, cancellationToken);

    public void AddExternalLogin(ExternalLogin link) => db.ExternalLogins.Add(link);

    public void RemoveCredential(Credential credential) => db.Credentials.Remove(credential);
}
