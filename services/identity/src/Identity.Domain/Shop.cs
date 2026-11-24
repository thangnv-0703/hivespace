namespace Identity.Domain;

/// <summary>A seller's shop. Its own aggregate, committed together with the owner's Seller grant (FEAT-009 D-1).</summary>
public sealed class Shop
{
    private Shop()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        SellerType = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    /// <summary>One of the <see cref="Domain.SellerType"/> values.</summary>
    public string SellerType { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>UUID v7 id (ADR-0009).</summary>
    public static Shop Register(Guid ownerUserId, ShopName name, SellerType type, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OwnerUserId = ownerUserId,
            Name = name.Value,
            NormalizedName = name.Normalized,
            SellerType = type.Value,
            CreatedAt = now,
            UpdatedAt = now,
        };
}
