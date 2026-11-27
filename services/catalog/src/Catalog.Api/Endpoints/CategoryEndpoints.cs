using System.Globalization;
using Catalog.Application.Features.Categories;
using Catalog.Application.Features.Ping;
using HiveSpace.BuildingBlocks.Abstractions;
using HiveSpace.BuildingBlocks.Errors;

namespace Catalog.Api.Endpoints;

/// <summary>REST endpoints for <c>contracts/openapi/catalog.yaml</c>. Thin: they call Application handlers.</summary>
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // parentId is read as text so a malformed one is a 404 like an unknown one (design §3.1), not a binding error.
        app.MapGet("/v1/categories", async (string? parentId, ListCategoriesHandler handler, CancellationToken ct) =>
            {
                if (parentId is null)
                {
                    return Results.Ok((await handler.HandleAsync(null, ct)).OrThrow());
                }

                return int.TryParse(parentId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                    ? Results.Ok((await handler.HandleAsync(id, ct)).OrThrow())
                    : throw new ApiException(ErrorCodes.NotFound);
            })
            .WithName("listCategories");

        app.MapGet("/v1/categories/{id}", async (string id, GetCategoryHandler handler, CancellationToken ct) =>
                int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var categoryId)
                    ? Results.Ok((await handler.HandleAsync(categoryId, ct)).OrThrow())
                    : throw new ApiException(ErrorCodes.NotFound))
            .WithName("getCategory");

        app.MapPost("/v1/catalog/ping", async (HttpContext http, PublishPingHandler handler, CancellationToken ct) =>
                Results.Accepted(value: new { eventId = await handler.HandleAsync(http.GetCorrelationId(), ct) }))
            .WithName("publishPing");

        return app;
    }
}
