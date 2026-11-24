namespace Identity.Domain;

/// <summary>
/// An external identity (for now Google) linked to a user, keyed by the provider's stable <c>sub</c> (FR-AUTH-020, D-4).
/// The provider's email is not stored: the account's own email is the one address (NFR-LEG-001).
/// </summary>
public sealed class ExternalLogin
{
    public const int ProviderMaxLength = 32;
    public const int SubjectMaxLength = 255;

    private ExternalLogin()
    {
        Provider = string.Empty;
        Subject = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Provider { get; private set; }

    public string Subject { get; private set; }

    public DateTimeOffset LinkedAt { get; private set; }

    public static ExternalLogin Link(Guid userId, string provider, string subject, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            Provider = provider,
            Subject = subject,
            LinkedAt = now,
        };
}
