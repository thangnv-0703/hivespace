namespace Identity.Domain;

/// <summary>A marketplace account. Starts unverified with the Buyer role (FR-AUTH-001, FR-ROLE-001).</summary>
public sealed class User
{
    private readonly List<RoleGrant> _roles = [];

    private User()
    {
        EmailAddress = string.Empty;
        NormalizedEmail = string.Empty;
        DisplayName = string.Empty;
        Language = "vi";
    }

    public Guid Id { get; private set; }

    public string EmailAddress { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string DisplayName { get; private set; }

    public string Language { get; private set; }

    public DateTimeOffset? EmailVerifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RoleGrant> Roles => _roles;

    public bool IsEmailVerified => EmailVerifiedAt is not null;

    /// <summary>UUID v7 id (ADR-0009); language is the registration language that drives the email (D-2).</summary>
    public static User Register(Email email, DisplayName displayName, string language, DateTimeOffset now)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            EmailAddress = email.Value,
            NormalizedEmail = email.Normalized,
            DisplayName = displayName.Value,
            Language = language is "en" ? "en" : "vi",
            CreatedAt = now,
            UpdatedAt = now,
        };
        user._roles.Add(RoleGrant.Create(user.Id, Role.Buyer, now));
        return user;
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        EmailVerifiedAt ??= now;
        UpdatedAt = now;
    }
}
