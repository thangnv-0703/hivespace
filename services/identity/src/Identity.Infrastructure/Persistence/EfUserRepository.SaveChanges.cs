using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository
{
    private const string UniqueViolation = "23505";

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
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "ix_shops_owner_user_id" or "PK_role_grants" })
        {
            db.ChangeTracker.Clear();
            // A parallel registration by the same user committed first: it won the shop index or the Seller grant, whichever the batch reached first.
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
