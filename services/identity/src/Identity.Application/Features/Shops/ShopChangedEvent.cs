using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Shops;

/// <summary>
/// Payload of <c>ShopChanged/v1</c> (contracts/events, FEAT-010 D-6): the public profile, the readiness flag and the version.
/// Never the pickup address, the phone or any bank field (FR-SHOP-009).
/// </summary>
public sealed record ShopChangedData(
    Guid ShopId,
    Guid OwnerUserId,
    string Name,
    string? Description,
    string SellerType,
    bool SetupComplete,
    int Version,
    DateTimeOffset ChangedAt);

/// <summary>What the three shop-settings handlers share: queueing <c>ShopChanged</c>, the validation list and the bounded retry.</summary>
internal static class ShopSettings
{
    public const string ShopChangedEvent = "ShopChanged";

    private const int Attempts = 3;

    public static void QueueShopChanged(IEventOutbox outbox, Domain.Shop shop, DateTimeOffset now) =>
        outbox.Enqueue(
            ShopChangedEvent,
            1,
            shop.Id.ToString(),
            new ShopChangedData(shop.Id, shop.OwnerUserId, shop.Name, shop.Description, shop.SellerType, shop.SetupComplete, shop.ProfileVersion, now));

    /// <summary>
    /// The unknown keys (and keys of the wrong type) first, as <c>common.validation_failed</c> on that key; a field already reported that
    /// way is not validated again, so each field has one entry.
    /// </summary>
    public static List<Violation> StartViolations(IReadOnlyList<string> invalidKeys) =>
        invalidKeys.Distinct().Select(key => new Violation(key, ErrorCodes.ValidationFailed)).ToList();

    public static void Add(List<Violation> violations, Violation? violation)
    {
        if (violation is not null && violations.All(v => v.Field != violation.Field))
        {
            violations.Add(violation);
        }
    }

    public static void Add(List<Violation> violations, IEnumerable<Violation> found)
    {
        foreach (var violation in found)
        {
            Add(violations, violation);
        }
    }

    /// <summary>
    /// Runs <paramref name="attempt"/> again when a parallel write changed the shop row (the row version), up to three times; after that the
    /// exception surfaces (FEAT-010 §3.1, D-9).
    /// </summary>
    public static async Task<Result<MyShopView>> WithRetryAsync(Func<Task<Result<MyShopView>>> attempt)
    {
        for (var n = 1; ; n++)
        {
            try
            {
                return await attempt().ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException) when (n < Attempts)
            {
                // Decide again on a fresh read.
            }
        }
    }
}
