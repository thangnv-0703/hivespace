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

    public static Credential Create(Guid userId, string passwordHash, DateTimeOffset now) =>
        new() { UserId = userId, PasswordHash = passwordHash, UpdatedAt = now };
}
