using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>The pickup address form; see <see cref="UpdateShopProfileCommand"/> for <paramref name="InvalidKeys"/>.</summary>
public sealed record SetPickupAddressCommand(
    Guid UserId,
    string? ContactName,
    string? Phone,
    string? Province,
    string? District,
    string? Ward,
    string? Street,
    IReadOnlyList<string> InvalidKeys);

/// <summary>FR-SHOP-006: saves the pickup address; queues <c>ShopChanged</c> only when it makes the shop ready to sell (D-6).</summary>
public sealed class SetPickupAddressHandler(IShopStore shops, IUserRepository users, IEventOutbox outbox, TimeProvider clock)
{
    public Task<Result<MyShopView>> HandleAsync(SetPickupAddressCommand command, CancellationToken cancellationToken) =>
        ShopSettings.WithRetryAsync(() => HandleOnceAsync(command, cancellationToken));

    private async Task<Result<MyShopView>> HandleOnceAsync(SetPickupAddressCommand command, CancellationToken cancellationToken)
    {
        var violations = ShopSettings.StartViolations(command.InvalidKeys);
        var (address, found) = PickupAddress.Create(command.ContactName, command.Phone, command.Province, command.District, command.Ward, command.Street);
        ShopSettings.Add(violations, found);
        if (violations.Count > 0)
        {
            return Result<MyShopView>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var shop = await shops.FindByOwnerAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (shop is null)
        {
            return Result<MyShopView>.Fail(ErrorCodes.NotFound);
        }

        var now = clock.GetUtcNow();
        if (shop.SetPickupAddress(address!, now))
        {
            ShopSettings.QueueShopChanged(outbox, shop, now);
        }

        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MyShopView>.Ok(MyShopView.From(shop));
    }
}
