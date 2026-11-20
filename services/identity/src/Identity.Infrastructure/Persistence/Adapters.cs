using HiveSpace.BuildingBlocks.Messaging;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

internal sealed class EfUserRepository(IdentityDbContext db) : IUserRepository
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: UniqueViolation, ConstraintName: "IX_users_normalized_email" })
        {
            throw new EmailInUseException(ex);
        }
    }
}

/// <summary>Adds the outbox row to the same <see cref="IdentityDbContext"/>, so it commits with the user (ADR-0002, AC-6).</summary>
internal sealed class EfEventOutbox(IdentityDbContext db) : IEventOutbox
{
    public void Enqueue<T>(string eventType, int version, string correlationId, T data) =>
        db.Set<OutboxMessage>().Add(OutboxMessage.From(EventEnvelope.Create(eventType, version, "identity", correlationId, data)));
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
