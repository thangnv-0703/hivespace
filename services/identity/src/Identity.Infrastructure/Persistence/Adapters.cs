using System.Diagnostics;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

internal sealed class EfUserRepository(IdentityDbContext db) : IUserRepository, ILockoutStore, IAuditLog
{
    private const string UniqueViolation = "23505";

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

    public void Add(User user) => db.Users.Add(user);

    public Task<ExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken) =>
        db.ExternalLogins.FirstOrDefaultAsync(l => l.Provider == provider && l.Subject == subject, cancellationToken);

    public Task<ExternalLogin?> FindExternalLoginByUserAsync(Guid userId, string provider, CancellationToken cancellationToken) =>
        db.ExternalLogins.FirstOrDefaultAsync(l => l.UserId == userId && l.Provider == provider, cancellationToken);

    public void AddExternalLogin(ExternalLogin link) => db.ExternalLogins.Add(link);

    public void RemoveCredential(Credential credential) => db.Credentials.Remove(credential);

    public Task<SignInLockout?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.SignInLockouts.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

    public void Add(SignInLockout lockout) => db.SignInLockouts.Add(lockout);

    public void Remove(SignInLockout lockout) => db.SignInLockouts.Remove(lockout);

    public void Add(AuditEntry entry)
    {
        // Joins the row to the trace of the request (design §5.2); never an email, address or token (D-4).
        entry.SetCorrelationId(Activity.Current?.TraceId.ToString());
        db.AuditEntries.Add(entry);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "PK_sign_in_lockouts" })
        {
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "IX_users_normalized_email" })
        {
            db.ChangeTracker.Clear();
            throw new EmailInUseException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: string name }
            && name.StartsWith("IX_external_logins_", StringComparison.Ordinal))
        {
            db.ChangeTracker.Clear();
            throw new ExternalLoginConflictException(ex);
        }
    }
}
