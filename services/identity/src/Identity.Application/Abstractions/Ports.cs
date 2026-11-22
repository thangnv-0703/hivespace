using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>Why a handler refused: a stable error code and, for validation, every unmet rule.</summary>
public sealed record Failure(string Code, IReadOnlyList<Violation>? Violations = null);

/// <summary>The outcome of a handler: a value or a <see cref="Failure"/>. The Api maps failures to the error envelope.</summary>
public sealed record Result<T>
{
    private Result()
    {
    }

    public T? Value { get; private init; }

    public Failure? Failure { get; private init; }

    public bool IsSuccess => Failure is null;

    public static Result<T> Ok(T value) => new() { Value = value };

    public static Result<T> Fail(string code, IReadOnlyList<Violation>? violations = null) =>
        new() { Failure = new Failure(code, violations) };
}

/// <summary>Marker value for handlers that return nothing.</summary>
public readonly record struct Unit;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

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

/// <summary>Queues an event in the caller's unit of work; it commits with <see cref="IUserRepository.SaveChangesAsync"/> (ADR-0002).</summary>
public interface IEventOutbox
{
    void Enqueue<T>(string eventType, int version, string correlationId, T data);
}

/// <summary>Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two registrations race for one normalized email.</summary>
public sealed class EmailInUseException(Exception inner) : Exception("The email is already registered.", inner);

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

    void AddToken(VerificationToken token);

    /// <summary>Commits everything added or changed, including queued outbox rows, in one transaction.</summary>
    /// <exception cref="EmailInUseException">The unique index on the normalized email rejected the insert.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
