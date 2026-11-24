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
