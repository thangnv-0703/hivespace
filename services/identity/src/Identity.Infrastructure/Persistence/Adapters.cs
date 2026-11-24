using System.Diagnostics;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository(IdentityDbContext db, ILogger<EfUserRepository>? logger = null) : IUserRepository, ILockoutStore, IAuditLog, ISignInChallengeStore, IShopStore
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

    public void AddCredential(Credential credential) => db.Credentials.Add(credential);

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

    Task<SignInCodeChallenge?> ISignInChallengeStore.FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.SignInCodeChallenges.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<SignInCodeChallenge?> FindNewestAsync(byte[] emailHash, CancellationToken cancellationToken) =>
        db.SignInCodeChallenges
            .Where(c => c.EmailHash == emailHash)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SignInCodeChallenge>> FindOpenAsync(byte[] emailHash, CancellationToken cancellationToken) =>
        await db.SignInCodeChallenges
            .Where(c => c.EmailHash == emailHash && c.EndedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(SignInCodeChallenge challenge) => db.SignInCodeChallenges.Add(challenge);

    public Task<Shop?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        db.Shops.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
        db.Shops.AnyAsync(s => s.NormalizedName == normalizedName, cancellationToken);

    public void Add(Shop shop) => db.Shops.Add(shop);

    public void Add(AuditEntry entry)
    {
        // Joins the row to the trace of the request (design §5.2); never an email, address or token (D-4).
        entry.SetCorrelationId(Activity.Current?.TraceId.ToString());
        db.AuditEntries.Add(entry);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var locked = db.ChangeTracker.Entries<AuditEntry>()
            .Where(e => e.State == EntityState.Added && e.Entity.Type == AuditEntry.AccountLockedType)
            .Select(e => e.Entity.UserId)
            .ToList();
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            // After the commit, so a retried save logs once. The user id only, never the email (NFR-SEC-008 spirit, design §5.2).
            if (logger is not null)
            {
                foreach (var userId in locked)
                {
                    LogAccountLocked(logger, userId);
                }
            }
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
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "ix_sign_in_code_challenges_open" })
        {
            db.ChangeTracker.Clear();
            throw new SignInChallengeConflictException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "PK_credentials" })
        {
            db.ChangeTracker.Clear();
            throw new CredentialExistsException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "IX_users_normalized_email" })
        {
            db.ChangeTracker.Clear();
            throw new EmailInUseException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "ix_shops_owner_user_id" })
        {
            db.ChangeTracker.Clear();
            throw new ShopAlreadyOwnedException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "ix_shops_normalized_name" })
        {
            db.ChangeTracker.Clear();
            throw new ShopNameTakenException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: string name }
            && name.StartsWith("IX_external_logins_", StringComparison.Ordinal))
        {
            db.ChangeTracker.Clear();
            throw new ExternalLoginConflictException(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Account locked: user {UserId}")]
    private static partial void LogAccountLocked(ILogger logger, Guid? userId);
}
