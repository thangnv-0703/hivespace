namespace Identity.Domain;

/// <summary>A password credential (argon2id PHC string). Google-only accounts have none (D-8).</summary>
public sealed class Credential
{
    private Credential()
    {
        PasswordHash = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The password stamp (D-4): <see cref="UpdatedAt"/> as Unix milliseconds. Milliseconds, because Postgres keeps microseconds and .NET 100 ns ticks.</summary>
    public long Stamp => UpdatedAt.ToUnixTimeMilliseconds();

    public void ChangePassword(string newHash, DateTimeOffset now)
    {
        PasswordHash = newHash;
        UpdatedAt = now;
    }

    public static Credential Create(Guid userId, string passwordHash, DateTimeOffset now) =>
        new() { UserId = userId, PasswordHash = passwordHash, UpdatedAt = now };
}
