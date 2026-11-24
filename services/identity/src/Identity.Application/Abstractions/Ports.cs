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

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when a credential insert hits the primary key: the account got a
/// password between the check and the save (two parallel set-password calls, FR-AUTH-027).
/// </summary>
public sealed class CredentialExistsException(Exception inner) : Exception("The account already has a password.", inner);

/// <summary>Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two registrations race for one normalized email.</summary>
public sealed class EmailInUseException(Exception inner) : Exception("The email is already registered.", inner);

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when an external login insert hits one of its unique indexes:
/// two first sign-ins for one identity raced, or an account already holds an identity of the provider (AC-11).
/// </summary>
public sealed class ExternalLoginConflictException(Exception inner) : Exception("The external login is already linked.", inner);

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two failed attempts for one account raced: the lockout row changed
/// or was inserted since it was read. Nothing stays staged, so the caller reads it again and reapplies (FEAT-005 D-3).
/// </summary>
public sealed class ConcurrencyConflictException(Exception inner) : Exception("A lockout or challenge changed concurrently.", inner);

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the "one open challenge per address" index rejects an insert:
/// another code request for the address committed first (FEAT-006 §3.2).
/// </summary>
public sealed class SignInChallengeConflictException(Exception inner) : Exception("Another sign-in code was issued for the address.", inner);

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the unique index on <c>shops.owner_user_id</c> rejects an insert:
/// the user registered a shop in a parallel request (FR-SHOP-002).
/// </summary>
public sealed class ShopAlreadyOwnedException(Exception inner) : Exception("The user already owns a shop.", inner);

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the unique index on <c>shops.normalized_name</c> rejects an insert:
/// another shop took the name first (FR-SHOP-003).
/// </summary>
public sealed class ShopNameTakenException(Exception inner) : Exception("The shop name is already taken.", inner);

/// <summary>Shops (FEAT-009). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface IShopStore
{
    Task<Shop?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken);

    void Add(Shop shop);
}

/// <summary>Six-digit sign-in codes and the hashes stored for them (NFR-SEC-016, FEAT-006 D-7).</summary>
public interface ISignInCodeGenerator
{
    /// <summary>Six digits from a cryptographically secure generator.</summary>
    string NewCode();

    /// <summary>The stored hash of a code, salted with its challenge id.</summary>
    byte[] HashCode(Guid challengeId, string code);

    /// <summary>Compares a presented code with the stored hash in constant time.</summary>
    bool Matches(Guid challengeId, string code, byte[] codeHash);

    /// <summary>The stored hash of a normalized email; the plain address is never stored for a challenge (D-3).</summary>
    byte[] HashEmail(string normalizedEmail);
}

/// <summary>Sign-in code challenges (FR-AUTH-028). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface ISignInChallengeStore
{
    Task<SignInCodeChallenge?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The most recently created challenge for the address, in any state.</summary>
    Task<SignInCodeChallenge?> FindNewestAsync(byte[] emailHash, CancellationToken cancellationToken);

    /// <summary>The challenges of the address that have not ended.</summary>
    Task<IReadOnlyList<SignInCodeChallenge>> FindOpenAsync(byte[] emailHash, CancellationToken cancellationToken);

    void Add(SignInCodeChallenge challenge);
}

/// <summary>Lockout rows (FR-AUTH-011). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface ILockoutStore
{
    Task<SignInLockout?> FindAsync(Guid userId, CancellationToken cancellationToken);

    void Add(SignInLockout lockout);

    void Remove(SignInLockout lockout);
}

/// <summary>The security audit log (NFR-SEC-009); rows are committed with whatever else the unit of work holds.</summary>
public interface IAuditLog
{
    void Add(AuditEntry entry);
}

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

    /// <summary>A first password for an account that has none (FR-AUTH-027).</summary>
    void AddCredential(Credential credential);

    /// <summary>
    /// Commits everything added or changed, including queued outbox rows, in one transaction.
    /// On a unique-index conflict nothing stays staged, so the caller can decide again on fresh reads.
    /// </summary>
    /// <exception cref="EmailInUseException">The unique index on the normalized email rejected the insert.</exception>
    /// <exception cref="ExternalLoginConflictException">A unique index of <c>external_logins</c> rejected the insert.</exception>
    /// <exception cref="CredentialExistsException">The primary key of <c>credentials</c> rejected the insert.</exception>
    /// <exception cref="ConcurrencyConflictException">The lockout row of the account, or a sign-in code challenge, changed or appeared since it was read.</exception>
    /// <exception cref="SignInChallengeConflictException">The open-challenge index of <c>sign_in_code_challenges</c> rejected the insert.</exception>
    /// <exception cref="ShopAlreadyOwnedException">The unique index on <c>shops.owner_user_id</c> rejected the insert.</exception>
    /// <exception cref="ShopNameTakenException">The unique index on <c>shops.normalized_name</c> rejected the insert.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
