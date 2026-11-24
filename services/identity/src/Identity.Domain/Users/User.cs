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

    /// <summary>E.164 <c>+84xxxxxxxxx</c> or null (FR-PROF-002).</summary>
    public string? Phone { get; private set; }

    /// <summary>One of the <see cref="Domain.Gender"/> values, or null.</summary>
    public string? Gender { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

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

    /// <summary>An account created by an external sign-in: the provider proved the email, so it starts verified (FR-AUTH-020).</summary>
    public static User RegisterExternal(Email email, DisplayName displayName, string language, DateTimeOffset now)
    {
        var user = Register(email, displayName, language, now);
        user.EmailVerifiedAt = now;
        return user;
    }

    /// <summary>Adds the role when the user lacks it and returns whether it did; existing roles (Buyer) are never removed (FR-ROLE-002).</summary>
    public bool GrantRole(Role role, DateTimeOffset now)
    {
        if (_roles.Any(r => r.Role == role))
        {
            return false;
        }

        _roles.Add(RoleGrant.Create(Id, role, now));
        UpdatedAt = now;
        return true;
    }

    public void VerifyEmail(DateTimeOffset now)
    {
        EmailVerifiedAt ??= now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Replaces the four editable fields (PUT semantics; null clears the optional ones). Returns the changed fields that
    /// <c>UserProfileChanged</c> carries: only <c>displayName</c> can appear, phone, gender and date of birth never leave Identity (D-6).
    /// </summary>
    public IReadOnlyList<string> UpdateProfile(DisplayName displayName, PhoneNumber? phone, Gender? gender, DateOfBirth? dateOfBirth, DateTimeOffset now)
    {
        var changed = new List<string>();
        var edited = DisplayName != displayName.Value
            || Phone != phone?.Value
            || Gender != gender?.Value
            || DateOfBirth != dateOfBirth?.Value;

        if (DisplayName != displayName.Value)
        {
            changed.Add("displayName");
        }

        DisplayName = displayName.Value;
        Phone = phone?.Value;
        Gender = gender?.Value;
        DateOfBirth = dateOfBirth?.Value;
        if (edited)
        {
            UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>Saves the preferred language; returns <c>["language"]</c> when it changed, otherwise empty (idempotent).</summary>
    public IReadOnlyList<string> ChangeLanguage(Language language, DateTimeOffset now)
    {
        if (Language == language.Value)
        {
            return [];
        }

        Language = language.Value;
        UpdatedAt = now;
        return ["language"];
    }
}
