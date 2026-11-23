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
    /// Vite dev servers, local only (repo-structure §5). The gateway proxies to them by host (ADR-0027): localhost:5080 is the
    /// storefront, seller.localhost:5080 the seller centre, so the browser sees the same hosts in development as in production.
    /// Each app gets the other's origin for its cross-app link.
    /// </summary>
    public static WebResources AddWeb(this IDistributedApplicationBuilder builder)
    {
        // One workspace install: per-app installers run concurrently on the same node_modules and fail with EPERM.
        var install = builder.AddExecutable("web-installer", FindPnpm(), "../../web", "install", "--frozen-lockfile");
        var storefront = builder.AddViteApp("storefront", "../../web/apps/storefront")
            .WithPnpm(install: false)
            .WithEndpoint("http", endpoint => endpoint.Port = StorefrontPort)
            .WithEnvironment("VITE_SELLER_ORIGIN", DotnetServicesExtensions.SellerOrigin)
            .WaitForCompletion(install);
        var sellerCentre = builder.AddViteApp("seller-centre", "../../web/apps/seller-centre")
            .WithPnpm(install: false)
            .WithEndpoint("http", endpoint => endpoint.Port = SellerCentrePort)
            .WithEnvironment("VITE_STOREFRONT_ORIGIN", DotnetServicesExtensions.PublicOrigin)
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
        InfrastructureResources infra,
        DotnetResources dotnet,
        GoResources go,
        PythonResources python,
        WebResources web)
    {
        var gateway = builder.AddProject<Projects.Gateway>("gateway")
            .WithExternalHttpEndpoints()
            .WithReference(dotnet.Catalog)
            .WithReference(dotnet.Identity)
            .WithReference(infra.Blobs)
            .WithEnvironment("Identity__Issuer", DotnetServicesExtensions.PublicOrigin)
            .WithOidcClients(dotnet.IdentitySecrets)
            .WaitFor(infra.Blobs)
            .WithReference(go.Cart)
            .WithReference(python.SearchAi)
            .WithReference(web.Storefront)
            .WithReference(web.SellerCentre)
            .WaitFor(dotnet.Catalog)
            .WaitFor(dotnet.Identity);

        foreach (var service in dotnet.Others)
        {
            gateway.WithReference(service);
        }

        // Optional, for the stacks that sign in many users from one loopback address (tests/e2e, tests/system): without it the gateway
        // enforces the real NFR-SEC-005 limit of 10 per minute (FEAT-005 D-9). Set as `RateLimits__PermitLimit` or `RateLimits:PermitLimit`.
        if (builder.Configuration["RateLimits:PermitLimit"] is { Length: > 0 } permitLimit)
        {
            gateway.WithEnvironment("RateLimits__PermitLimit", permitLimit);
        }

        // Deployed to Container Apps, the ingress appends the client address to X-Forwarded-For; the rate limits need it (FEAT-005 D-6).
        // Locally nothing trusts the header.
        if (builder.ExecutionContext.IsPublishMode)
        {
            gateway.WithEnvironment("Gateway__TrustForwardedFor", "true");
        }

        return gateway;
    }
}
