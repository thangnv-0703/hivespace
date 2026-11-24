using System.Text;
using System.Text.RegularExpressions;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// A shop name: trimmed, inner whitespace collapsed to one space, stored in NFC, 3 to 60 characters, no control characters (FEAT-009 D-4).
/// <see cref="Normalized"/> (lower-case invariant) is what uniqueness is checked on (FR-SHOP-003); it keeps accents.
/// </summary>
public sealed partial record ShopName
{
    public const int MinLength = 3;
    public const int MaxLength = 60;

    private ShopName(string value) => Value = value;

    public string Value { get; }

    public string Normalized => Value.Normalize(NormalizationForm.FormC).ToLowerInvariant();

    public static (ShopName? Name, Violation? Violation) Create(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return (null, new Violation("name", "identity.shop_name_required"));
        }

        // Checked before collapsing, so a tab or line break inside the name is rejected rather than turned into a space.
        if (trimmed.Any(char.IsControl))
        {
            return (null, new Violation("name", "identity.shop_name_invalid"));
        }

        var value = Whitespace().Replace(trimmed, " ").Normalize(NormalizationForm.FormC);

        if (value.Length < MinLength)
        {
            return (null, new Violation("name", "identity.shop_name_too_short"));
        }

        if (value.Length > MaxLength)
        {
            return (null, new Violation("name", "identity.shop_name_too_long"));
        }

        return (new ShopName(value), null);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
