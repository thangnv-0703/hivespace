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

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when an external login insert hits one of its unique indexes:
/// two first sign-ins for one identity raced, or an account already holds an identity of the provider (AC-11).
/// </summary>
public sealed class ExternalLoginConflictException(Exception inner) : Exception("The external login is already linked.", inner);

/// <summary>
/// Signs the user out of every session except one (FR-AUTH-024). It only stages changes in the same unit of work;
/// <see cref="IUserRepository.SaveChangesAsync"/> commits them with the credential and the outbox row (D-2).
/// </summary>
public interface ISessionRevoker
{
    /// <param name="keepAuthorizationId">The authorization to keep; null revokes all of them.</param>
    Task RevokeOtherSessionsAsync(Guid userId, string? keepAuthorizationId, CancellationToken cancellationToken);
}

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

    /// <summary>A user with no credential and no verification token (an external sign-in, FR-AUTH-020).</summary>
    void Add(User user);

    Task<ExternalLogin?> FindExternalLoginAsync(string provider, string subject, CancellationToken cancellationToken);

    Task<ExternalLogin?> FindExternalLoginByUserAsync(Guid userId, string provider, CancellationToken cancellationToken);

    void AddExternalLogin(ExternalLogin link);

    /// <summary>Deletes the password (FEAT-004 D-5); the account then signs in with its external login only.</summary>
    void RemoveCredential(Credential credential);

    void AddToken(VerificationToken token);

    /// <summary>
    /// Commits everything added or changed, including queued outbox rows, in one transaction.
    /// On a unique-index conflict nothing stays staged, so the caller can decide again on fresh reads.
    /// </summary>
    /// <exception cref="EmailInUseException">The unique index on the normalized email rejected the insert.</exception>
    /// <exception cref="ExternalLoginConflictException">A unique index of <c>external_logins</c> rejected the insert.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
