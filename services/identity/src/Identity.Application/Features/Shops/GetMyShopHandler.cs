using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;

namespace Identity.Application.Features.Shops;

/// <summary>FR-ROLE-003: the caller's own shop. The role check is the endpoint policy; a Seller without a shop is <c>common.not_found</c>.</summary>
public sealed class GetMyShopHandler(IShopStore shops)
{
    public async Task<Result<MyShopView>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var shop = await shops.FindByOwnerAsync(userId, cancellationToken).ConfigureAwait(false);
        return shop is null ? Result<MyShopView>.Fail(ErrorCodes.NotFound) : Result<MyShopView>.Ok(MyShopView.From(shop));
    }
}
