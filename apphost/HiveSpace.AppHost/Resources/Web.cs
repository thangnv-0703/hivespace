using Aspire.Hosting.JavaScript;

namespace HiveSpace.AppHost.Resources;

internal sealed record WebResources(IResourceBuilder<ViteAppResource> Storefront, IResourceBuilder<ViteAppResource> SellerCentre);

internal static class WebExtensions
{
    // Fixed dev ports, the same defaults as each app's vite.config.ts, so the apps can also be opened directly.
    // Not 5173-5175: those are the Vite defaults used by the older hivespace.web project on this machine.
    private const int StorefrontPort = 5180;
    private const int SellerCentrePort = 5181;

    /// <summary>
    /// Vite dev servers, local only (repo-structure §5). The gateway proxies "/" and "/seller" to them,
    /// so the browser sees one origin in development as it does in production.
    /// </summary>
    public static WebResources AddWeb(this IDistributedApplicationBuilder builder)
    {
        // One workspace install: per-app installers run concurrently on the same node_modules and fail with EPERM.
        var install = builder.AddExecutable("web-installer", FindPnpm(), "../../web", "install", "--frozen-lockfile");
        var storefront = builder.AddViteApp("storefront", "../../web/apps/storefront")
            .WithPnpm(install: false)
            .WithEndpoint("http", endpoint => endpoint.Port = StorefrontPort)
            .WaitForCompletion(install);
        var sellerCentre = builder.AddViteApp("seller-centre", "../../web/apps/seller-centre")
            .WithPnpm(install: false)
            .WithEndpoint("http", endpoint => endpoint.Port = SellerCentrePort)
            .WaitForCompletion(install);
        return new WebResources(storefront, sellerCentre);
    }

    /// <summary>
    /// The orchestrator does not resolve the Windows pnpm.cmd shim from a bare name, so find it on PATH here.
    /// </summary>
    private static string FindPnpm()
    {
        var name = OperatingSystem.IsWindows() ? "pnpm.cmd" : "pnpm";
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return name;
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
