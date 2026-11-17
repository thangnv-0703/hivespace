using Aspire.Hosting.JavaScript;

namespace HiveSpace.AppHost.Resources;

internal sealed record WebResources(IResourceBuilder<ViteAppResource> Storefront, IResourceBuilder<ViteAppResource> SellerCentre);

internal static class WebExtensions
{
    /// <summary>
    /// Vite dev servers, local only (repo-structure §5). The gateway proxies "/" and "/seller" to them,
    /// so the browser sees one origin in development as it does in production.
    /// </summary>
    public static WebResources AddWeb(this IDistributedApplicationBuilder builder)
    {
        var storefront = builder.AddViteApp("storefront", "../../web/apps/storefront")
            .WithPnpm();
        var sellerCentre = builder.AddViteApp("seller-centre", "../../web/apps/seller-centre")
            .WithPnpm();
        return new WebResources(storefront, sellerCentre);
    }

    public static IResourceBuilder<ProjectResource> AddGateway(
        this IDistributedApplicationBuilder builder,
        DotnetResources dotnet,
        GoResources go,
        PythonResources python,
        WebResources web)
    {
        var gateway = builder.AddProject<Projects.Gateway>("gateway")
            .WithExternalHttpEndpoints()
            .WithReference(dotnet.Catalog)
            .WithReference(go.Cart)
            .WithReference(python.SearchAi)
            .WithReference(web.Storefront)
            .WithReference(web.SellerCentre)
            .WaitFor(dotnet.Catalog);

        foreach (var service in dotnet.Others)
        {
            gateway.WithReference(service);
        }

        return gateway;
    }
}
