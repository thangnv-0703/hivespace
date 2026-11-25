using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>
/// The <c>MyShop</c> schema of <c>identity.yaml</c>. The payout carries the last four digits only (NFR-SEC-008): no field of this
/// view can hold the account number.
/// </summary>
public sealed record MyShopView(
    Guid ShopId,
    string Name,
    string SellerType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Description,
    PickupAddressView? PickupAddress,
    PayoutView? Payout,
    ShopSetupView Setup)
{
    public static MyShopView From(Domain.Shop shop) =>
        new(
            shop.Id,
            shop.Name,
            shop.SellerType,
            shop.CreatedAt,
            shop.UpdatedAt,
            shop.Description,
            shop.PickupAddress is { } a ? new PickupAddressView(a.ContactName, a.Phone, a.Province, a.District, a.Ward, a.Street) : null,
            shop.Payout is { } p ? new PayoutView(p.BankName.Value, p.Last4, p.AccountHolder.Value) : null,
            new ShopSetupView(shop.PickupAddress is not null, shop.Payout is not null));
}

public sealed record PickupAddressView(string ContactName, string Phone, string Province, string District, string Ward, string Street);

public sealed record PayoutView(string BankName, string AccountNumberLast4, string AccountHolder);

/// <summary>The setup checklist of the seller centre (SCR-SEL-002): each flag is true once that section is saved.</summary>
public sealed record ShopSetupView(bool PickupAddress, bool Payout);
