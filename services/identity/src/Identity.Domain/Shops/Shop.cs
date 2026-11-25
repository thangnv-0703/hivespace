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

    public string? Description { get; private set; }

    /// <summary>Starts at 1 with <c>ShopRegistered</c>; raised when the public profile or the readiness flag changes (<c>ShopChanged</c>, FEAT-010 D-6).</summary>
    public int ProfileVersion { get; private set; } = 1;

    public PickupAddress? PickupAddress { get; private set; }

    public PayoutDetails? Payout { get; private set; }

    /// <summary>True once both the pickup address and the payout are set (FR-SHOP-011).</summary>
    public bool SetupComplete => PickupAddress is not null && Payout is not null;

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

    /// <summary>Applies the public profile. Returns whether anything changed; only then is the version raised.</summary>
    public bool UpdateProfile(ShopName name, ShopDescription? description, DateTimeOffset now)
    {
        var newDescription = description?.Value;
        if (name.Value == Name && newDescription == Description)
        {
            return false;
        }

        Name = name.Value;
        NormalizedName = name.Normalized;
        Description = newDescription;
        ProfileVersion++;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Sets the pickup address. Returns true when this made the shop ready (the version is raised for the event).</summary>
    public bool SetPickupAddress(PickupAddress address, DateTimeOffset now)
    {
        var wasComplete = SetupComplete;
        PickupAddress = address;
        return Touch(wasComplete, now);
    }

    /// <summary>Sets the payout details. Returns true when this made the shop ready (the version is raised for the event).</summary>
    public bool SetPayout(PayoutDetails payout, DateTimeOffset now)
    {
        var wasComplete = SetupComplete;
        Payout = payout;
        return Touch(wasComplete, now);
    }

    private bool Touch(bool wasComplete, DateTimeOffset now)
    {
        UpdatedAt = now;
        if (wasComplete || !SetupComplete)
        {
            return false;
        }

        ProfileVersion++;
        return true;
    }
}
