using System.Security.Claims;
using System.Text.Json;
using HiveSpace.BuildingBlocks.Abstractions;
using HiveSpace.BuildingBlocks.Errors;
using Identity.Application.Features.Shops;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Api.Endpoints;

/// <summary>
/// The caller's own shop (<c>contracts/openapi/identity.yaml</c>, FEAT-009 §3.1). The owner is always the <c>sub</c> of the validated
/// bearer token: no id in the path, and <c>X-User-Id</c> or a body field never picks the user (FR-ROLE-004, NFR-SEC-006).
/// </summary>
public static partial class ShopEndpoints
{
    /// <summary>The <c>me</c> policy plus the Seller role claim of the access token (FR-ROLE-003, D-2).</summary>
    public const string SellerPolicy = "seller";

    private static readonly HashSet<string> KnownKeys = ["name", "sellerType"];

    private static readonly string[] ProfileKeys = ["name", "description"];
    private static readonly string[] AddressKeys = ["contactName", "phone", "province", "district", "ward", "street"];
    private static readonly string[] PayoutKeys = ["bankName", "accountNumber", "accountHolder"];

    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var shops = app.MapGroup("/v1/shops");

        shops.MapPost("/", async (ClaimsPrincipal user, JsonElement body, RegisterShopHandler handler, CancellationToken ct) =>
            {
                var shop = (await handler.HandleAsync(ToCommand(user.GetUserId(), body), ct)).OrThrow();
                return Results.Created("/v1/shops/me", shop);
            })
            .RequireAuthorization(MeEndpoints.Policy)
            .WithName("registerShop");

        shops.MapGet("/me", async (ClaimsPrincipal user, GetMyShopHandler handler, CancellationToken ct) =>
                Results.Ok((await handler.HandleAsync(user.GetUserId(), ct)).OrThrow()))
            .RequireAuthorization(SellerPolicy)
            .WithName("getMyShop");

        // Updates are PUT sub-resources, each the full representation of one section; every key must be present (FEAT-010 D-3).
        shops.MapPut("/me/profile", async (ClaimsPrincipal user, JsonElement body, UpdateShopProfileHandler handler, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var form = ReadForm(body, ProfileKeys, nullable: ["description"]);
                var shop = (await handler.HandleAsync(new UpdateShopProfileCommand(user.GetUserId(), form.Read("name"), form.Read("description"), form.Invalid), ct)).OrThrow();
                LogShopSaved(loggers.CreateLogger(typeof(ShopEndpoints)), shop.ShopId, "profile");
                return Results.Ok(shop);
            })
            .RequireAuthorization(SellerPolicy)
            .WithName("updateMyShopProfile");

        shops.MapPut("/me/pickup-address", async (ClaimsPrincipal user, JsonElement body, SetPickupAddressHandler handler, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var form = ReadForm(body, AddressKeys, nullable: []);
                var command = new SetPickupAddressCommand(
                    user.GetUserId(), form.Read("contactName"), form.Read("phone"), form.Read("province"), form.Read("district"), form.Read("ward"), form.Read("street"), form.Invalid);
                var shop = (await handler.HandleAsync(command, ct)).OrThrow();
                LogShopSaved(loggers.CreateLogger(typeof(ShopEndpoints)), shop.ShopId, "pickup-address");
                return Results.Ok(shop);
            })
            .RequireAuthorization(SellerPolicy)
            .WithName("setMyShopPickupAddress");

        shops.MapPut("/me/payout", async (ClaimsPrincipal user, JsonElement body, SetPayoutHandler handler, ILoggerFactory loggers, CancellationToken ct) =>
            {
                var form = ReadForm(body, PayoutKeys, nullable: []);
                var command = new SetPayoutCommand(user.GetUserId(), form.Read("bankName"), form.Read("accountNumber"), form.Read("accountHolder"), form.Invalid);
                var shop = (await handler.HandleAsync(command, ct)).OrThrow();
                LogShopSaved(loggers.CreateLogger(typeof(ShopEndpoints)), shop.ShopId, "payout");
                return Results.Ok(shop);
            })
            .RequireAuthorization(SellerPolicy)
            .WithName("setMyShopPayout");

        return app;
    }

    /// <summary>
    /// Reads a PUT body as a <see cref="JsonElement"/> (same reading as <c>PUT /v1/me/profile</c>). An unknown key, a missing nullable key
    /// and a value of the wrong JSON type go to <c>Invalid</c>; a missing or null required key reads as null and fails its own rule.
    /// </summary>
    private static (Func<string, string?> Read, List<string> Invalid) ReadForm(JsonElement body, string[] keys, string[] nullable)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw ApiException.Validation([new FieldError("body", ErrorCodes.ValidationFailed)]);
        }

        var invalid = body.EnumerateObject().Select(p => p.Name).Where(name => !keys.Contains(name)).ToList();
        invalid.AddRange(nullable.Where(key => !body.TryGetProperty(key, out _)));

        string? Read(string key)
        {
            if (!body.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (!invalid.Contains(key))
            {
                invalid.Add(key);
            }

            return null;
        }

        return (Read, invalid);
    }

    // The shop id and the section only: never the body (NFR-SEC-008).
    [LoggerMessage(Level = LogLevel.Information, Message = "Shop settings saved: shop {ShopId}, section {Section}")]
    private static partial void LogShopSaved(ILogger logger, Guid shopId, string section);

    /// <summary>
    /// Reads the body as a <see cref="JsonElement"/> so an unknown key becomes a field error instead of being dropped (same reading as
    /// <c>PUT /v1/me/profile</c>). A missing or wrongly typed value reaches validation as null.
    /// </summary>
    private static RegisterShopCommand ToCommand(Guid userId, JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw ApiException.Validation([new FieldError("body", ErrorCodes.ValidationFailed)]);
        }

        string? Read(string key) =>
            body.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        var unknown = body.EnumerateObject().Select(p => p.Name).Where(name => !KnownKeys.Contains(name)).ToList();
        return new RegisterShopCommand(userId, Read("name"), Read("sellerType"), unknown);
    }
}
