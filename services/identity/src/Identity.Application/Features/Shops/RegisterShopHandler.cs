using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>
/// The registration form. <paramref name="UnknownKeys"/> are body keys outside the contract, found by the Api before binding;
/// the owner is always <paramref name="UserId"/>, the token's <c>sub</c> (FR-ROLE-004, NFR-SEC-006).
/// </summary>
public sealed record RegisterShopCommand(Guid UserId, string? Name, string? SellerType, IReadOnlyList<string> UnknownKeys);

/// <summary>Payload of <c>ShopRegistered/v1</c> (contracts/events).</summary>
public sealed record ShopRegisteredData(Guid ShopId, Guid OwnerUserId, string Name, string SellerType, DateTimeOffset RegisteredAt);

/// <summary>
/// FR-SHOP-001..004, FR-ROLE-002: validates every field, checks the email is verified (from the database, D-3), the one-shop and
/// unique-name rules, then commits the shop, the Seller grant, the audit row and the outbox row in one transaction (D-1).
/// </summary>
public sealed class RegisterShopHandler(
    IUserRepository users,
    IShopStore shops,
    IAuditLog audit,
    IEventOutbox outbox,
    TimeProvider clock)
{
    public const string ShopRegisteredEvent = "ShopRegistered";

    public async Task<Result<MyShopView>> HandleAsync(RegisterShopCommand command, CancellationToken cancellationToken)
    {
        var violations = command.UnknownKeys.Select(key => new Violation(key, ErrorCodes.ValidationFailed)).ToList();
        var (name, nameViolation) = ShopName.Create(command.Name);
        var (sellerType, typeViolation) = SellerType.Create(command.SellerType);
        if (nameViolation is not null)
        {
            violations.Add(nameViolation);
        }

        if (typeViolation is not null)
        {
            violations.Add(typeViolation);
        }

        if (violations.Count > 0)
        {
            return Result<MyShopView>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var user = await users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<MyShopView>.Fail(ErrorCodes.Unauthenticated);
        }

        if (!user.IsEmailVerified)
        {
            return Result<MyShopView>.Fail("identity.email_not_verified");
        }

        if (await shops.FindByOwnerAsync(user.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result<MyShopView>.Fail("identity.shop_already_owned");
        }

        if (await shops.NameExistsAsync(name!.Normalized, cancellationToken).ConfigureAwait(false))
        {
            return Result<MyShopView>.Fail("identity.shop_name_taken");
        }

        var now = clock.GetUtcNow();
        var shop = Domain.Shop.Register(user.Id, name, sellerType!, now);
        shops.Add(shop);
        if (user.GrantRole(Role.Seller, now))
        {
            audit.Add(AuditEntry.SellerRoleGranted(user.Id, now));
        }

        outbox.Enqueue(
            ShopRegisteredEvent,
            1,
            shop.Id.ToString(),
            new ShopRegisteredData(shop.Id, user.Id, shop.Name, shop.SellerType, now));

        try
        {
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ShopAlreadyOwnedException)
        {
            return Result<MyShopView>.Fail("identity.shop_already_owned");
        }
        catch (ShopNameTakenException)
        {
            return Result<MyShopView>.Fail("identity.shop_name_taken");
        }

        return Result<MyShopView>.Ok(MyShopView.From(shop));
    }
}
