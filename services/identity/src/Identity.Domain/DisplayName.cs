namespace Identity.Domain;

/// <summary>The name shown on the account: trimmed, 1 to 80 characters (FR-AUTH-004, FR-PROF-003).</summary>
public sealed record DisplayName
{
    public const int MaxLength = 80;

    private DisplayName(string value) => Value = value;

    public string Value { get; }

    public static (DisplayName? Name, Violation? Violation) Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return (null, new Violation("displayName", "identity.display_name_required"));
        }

        if (value.Length > MaxLength)
        {
            return (null, new Violation("displayName", "identity.display_name_too_long"));
        }

        return (new DisplayName(value), null);
    }
}
