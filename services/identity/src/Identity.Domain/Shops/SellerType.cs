using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>Whether the seller trades as an individual or a business; recorded, never verified (FR-SHOP-004).</summary>
public sealed record SellerType
{
    public const string Individual = "individual";
    public const string Business = "business";

    private SellerType(string value) => Value = value;

    public string Value { get; }

    public static (SellerType? Type, Violation? Violation) Create(string? raw) =>
        raw is Individual or Business
            ? (new SellerType(raw), null)
            : (null, new Violation("sellerType", "identity.seller_type_invalid"));

    /// <summary>Rebuilds from a stored value (already validated on the way in).</summary>
    public static SellerType FromStored(string value) => new(value);
}
