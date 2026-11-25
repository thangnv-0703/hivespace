using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>
/// The shop profile form. <paramref name="InvalidKeys"/> are body keys the Api found to be unknown, missing (a nullable key still has to
/// be present) or of the wrong JSON type. The shop is always the one owned by <paramref name="UserId"/>, the token's <c>sub</c>.
/// </summary>
public sealed record UpdateShopProfileCommand(Guid UserId, string? Name, string? Description, IReadOnlyList<string> InvalidKeys);

/// <summary>FR-SHOP-005, FR-SHOP-003: renames and describes the shop; queues <c>ShopChanged</c> only when something changed (FEAT-010 §3.1).</summary>
public sealed class UpdateShopProfileHandler(IShopStore shops, IUserRepository users, IEventOutbox outbox, TimeProvider clock)
{
    public Task<Result<MyShopView>> HandleAsync(UpdateShopProfileCommand command, CancellationToken cancellationToken) =>
        ShopSettings.WithRetryAsync(() => HandleOnceAsync(command, cancellationToken));

    private async Task<Result<MyShopView>> HandleOnceAsync(UpdateShopProfileCommand command, CancellationToken cancellationToken)
    {
        var violations = ShopSettings.StartViolations(command.InvalidKeys);
        var (name, nameViolation) = ShopName.Create(command.Name);
        var (description, descriptionViolation) = ShopDescription.Create(command.Description);
        ShopSettings.Add(violations, nameViolation);
        ShopSettings.Add(violations, descriptionViolation);
        if (violations.Count > 0)
        {
            return Result<MyShopView>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var shop = await shops.FindByOwnerAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (shop is null)
        {
            return Result<MyShopView>.Fail(ErrorCodes.NotFound);
        }

        // The shop's own name, in any case or spacing, is not a conflict (FR-SHOP-003 kept on rename).
        if (name!.Normalized != shop.NormalizedName
            && await shops.NameExistsAsync(name.Normalized, cancellationToken).ConfigureAwait(false))
        {
            return Result<MyShopView>.Fail("identity.shop_name_taken");
        }

        var now = clock.GetUtcNow();
        if (shop.UpdateProfile(name, description, now))
        {
            ShopSettings.QueueShopChanged(outbox, shop, now);
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ShopNameTakenException)
        {
            return Result<MyShopView>.Fail("identity.shop_name_taken");
        }

        return Result<MyShopView>.Ok(MyShopView.From(shop));
    }
}
