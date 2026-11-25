using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>
/// The payout form. <see cref="ToString"/> is hidden on purpose: a command logged by accident must not print the account number
/// (NFR-SEC-008). See <see cref="UpdateShopProfileCommand"/> for <paramref name="InvalidKeys"/>.
/// </summary>
public sealed record SetPayoutCommand(Guid UserId, string? BankName, string? AccountNumber, string? AccountHolder, IReadOnlyList<string> InvalidKeys)
{
    public override string ToString() => $"{nameof(SetPayoutCommand)} {{ UserId = {UserId} }}";
}

/// <summary>
/// FR-SHOP-007: encrypts and saves the payout bank details; queues <c>ShopChanged</c> only when it makes the shop ready to sell (D-6).
/// No payout, transfer or verification is started.
/// </summary>
public sealed class SetPayoutHandler(IShopStore shops, IUserRepository users, IBankAccountProtector protector, IEventOutbox outbox, TimeProvider clock)
{
    public Task<Result<MyShopView>> HandleAsync(SetPayoutCommand command, CancellationToken cancellationToken) =>
        ShopSettings.WithRetryAsync(() => HandleOnceAsync(command, cancellationToken));

    private async Task<Result<MyShopView>> HandleOnceAsync(SetPayoutCommand command, CancellationToken cancellationToken)
    {
        var violations = ShopSettings.StartViolations(command.InvalidKeys);
        var (bankName, bankViolation) = BankName.Create(command.BankName);
        var (number, numberViolation) = BankAccountNumber.Create(command.AccountNumber);
        var (holder, holderViolation) = AccountHolder.Create(command.AccountHolder);
        ShopSettings.Add(violations, bankViolation);
        ShopSettings.Add(violations, numberViolation);
        ShopSettings.Add(violations, holderViolation);
        if (violations.Count > 0)
        {
            return Result<MyShopView>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var shop = await shops.FindByOwnerAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (shop is null)
        {
            return Result<MyShopView>.Fail(ErrorCodes.NotFound);
        }

        var payout = new PayoutDetails(bankName!, holder!, protector.Protect(number!, shop.Id), number!.Last4);
        var now = clock.GetUtcNow();
        if (shop.SetPayout(payout, now))
        {
            ShopSettings.QueueShopChanged(outbox, shop, now);
        }

        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MyShopView>.Ok(MyShopView.From(shop));
    }
}
