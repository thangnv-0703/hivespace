using Catalog.Application.Features.Categories;
using Catalog.Application.Features.Ping;

namespace Catalog.Api.Endpoints;

/// <summary>REST endpoints for <c>contracts/openapi/catalog.yaml</c>. Thin: they call Application handlers.</summary>
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/categories", async (ListCategoriesHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .WithName("listCategories");

        app.MapPost("/v1/catalog/ping", async (HttpContext http, PublishPingHandler handler, CancellationToken ct) =>
                Results.Accepted(value: new { eventId = await handler.HandleAsync(http.GetCorrelationId(), ct) }))
            .WithName("publishPing");

        return app;
    }
}
