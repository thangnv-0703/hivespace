namespace Identity.Domain;

/// <summary>Password rules (FR-AUTH-003, NFR-SEC-003): 8 to 128 characters with a letter and a digit. Returns every unmet rule.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static IReadOnlyList<Violation> Evaluate(string? password, string field = "password")
    {
        var value = password ?? string.Empty;
        var violations = new List<Violation>();
        if (value.Length < MinLength)
        {
            violations.Add(new Violation(field, "identity.password_too_short"));
        }

        if (value.Length > MaxLength)
        {
            violations.Add(new Violation(field, "identity.password_too_long"));
        }

        if (!value.Any(char.IsLetter))
        {
            violations.Add(new Violation(field, "identity.password_needs_letter"));
        }

        if (!value.Any(char.IsDigit))
        {
            violations.Add(new Violation(field, "identity.password_needs_digit"));
        }

        return violations;
    }
}
