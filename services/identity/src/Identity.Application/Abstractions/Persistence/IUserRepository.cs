using Identity.Domain;

namespace Identity.Application.Abstractions;

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
