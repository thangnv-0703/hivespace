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
public static class ShopEndpoints
{
    /// <summary>The <c>me</c> policy plus the Seller role claim of the access token (FR-ROLE-003, D-2).</summary>
    public const string SellerPolicy = "seller";

    private static readonly HashSet<string> KnownKeys = ["name", "sellerType"];

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

        return app;
    }

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
