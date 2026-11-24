using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>The <c>MyShop</c> schema of <c>identity.yaml</c>. Both setup flags stay false until FEAT-010 lets the owner add them (D-7).</summary>
public sealed record MyShopView(Guid ShopId, string Name, string SellerType, DateTimeOffset CreatedAt, ShopSetupView Setup)
{
    public static MyShopView From(Domain.Shop shop) =>
        new(shop.Id, shop.Name, shop.SellerType, shop.CreatedAt, new ShopSetupView(false, false));
}

/// <summary>The setup checklist of the seller centre (SCR-SEL-002).</summary>
public sealed record ShopSetupView(bool PickupAddress, bool Payout);
