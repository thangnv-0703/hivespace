using Identity.Domain;

namespace Identity.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>An argon2id PHC string (D-3).</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a password. With a null hash it still performs a full verification against a dummy hash,
    /// so unknown accounts take comparable time (FR-AUTH-010).
    /// </summary>
    bool Verify(string? hash, string password);
}

/// <summary>A freshly generated token: the value that goes in the email and the hash that is stored (R-6).</summary>
public sealed record GeneratedToken(string Token, byte[] Hash);

public interface IVerificationTokenGenerator
{
    GeneratedToken Generate();

    byte[] Hash(string token);
}

/// <summary>Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two registrations race for one normalized email.</summary>
public sealed class EmailInUseException(Exception inner) : Exception("The email is already registered.", inner);

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Credential?> FindCredentialAsync(Guid userId, CancellationToken cancellationToken);

    Task<VerificationToken?> FindTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>Tokens of the user that are neither used nor invalidated yet.</summary>
    Task<IReadOnlyList<VerificationToken>> FindPendingTokensAsync(Guid userId, CancellationToken cancellationToken);

    void Add(User user, Credential credential, VerificationToken token);

    void AddToken(VerificationToken token);

    /// <summary>Commits everything added or changed, including queued outbox rows, in one transaction.</summary>
    /// <exception cref="EmailInUseException">The unique index on the normalized email rejected the insert.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
