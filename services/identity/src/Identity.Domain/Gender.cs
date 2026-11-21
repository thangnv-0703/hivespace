namespace Identity.Domain;

/// <summary>The optional gender choice of a profile (FR-PROF-002): four fixed values.</summary>
public sealed record Gender
{
    public const string Male = "male";
    public const string Female = "female";
    public const string Other = "other";
    public const string PreferNotToSay = "prefer_not_to_say";

    private static readonly string[] Allowed = [Male, Female, Other, PreferNotToSay];

    private Gender(string value) => Value = value;

    public string Value { get; }

    public static (Gender? Gender, Violation? Violation) Create(string? raw) =>
        raw is not null && Array.IndexOf(Allowed, raw) >= 0
            ? (new Gender(raw), null)
            : (null, new Violation("gender", "identity.gender_invalid"));
}
