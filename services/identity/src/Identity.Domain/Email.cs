using System.Text.RegularExpressions;

namespace Identity.Domain;

/// <summary>A well-formed email address. Uniqueness is checked on <see cref="Normalized"/> (trimmed, lower-invariant).</summary>
public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value)
    {
        Value = value;
        Normalized = value.ToLowerInvariant();
    }

    public string Value { get; }

    public string Normalized { get; }

    /// <summary>Normalizes for lookups without validating (used by sign-in and resend).</summary>
    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant();

    public static (Email? Email, Violation? Violation) Create(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return (null, new Violation("email", "identity.email_required"));
        }

        if (value.Length > MaxLength || !Shape().IsMatch(value))
        {
            return (null, new Violation("email", "identity.email_invalid"));
        }

        return (new Email(value), null);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Shape();
}
