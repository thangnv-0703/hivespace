using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// The optional shop description (FR-SHOP-005): trimmed, at most 1000 characters, <c>\r\n</c> stored as <c>\n</c>, no other control
/// character. Empty or whitespace-only input means "no description" (<c>null</c> value, no violation).
/// </summary>
public sealed record ShopDescription
{
    public const int MaxLength = 1000;

    private ShopDescription(string value) => Value = value;

    public string Value { get; }

    public static (ShopDescription? Description, Violation? Violation) Create(string? raw)
    {
        var text = (raw ?? string.Empty).Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.Length == 0)
        {
            return (null, null);
        }

        if (text.Any(c => char.IsControl(c) && c != '\n'))
        {
            return (null, new Violation("description", "identity.shop_description_invalid"));
        }

        return text.Length > MaxLength
            ? (null, new Violation("description", "identity.shop_description_too_long"))
            : (new ShopDescription(text), null);
    }
}
