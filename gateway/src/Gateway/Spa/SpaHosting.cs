using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Gateway.Auth;

namespace Gateway.Spa;

/// <summary>
/// Static hosting for the two SPAs, one host each (ADR-0027): the seller host serves <c>wwwroot/seller</c>, every other
/// host the storefront in <c>wwwroot</c>. Only active when wwwroot exists (the production image); in development YARP
/// forwards to Vite instead.
/// </summary>
internal static class SpaHosting
{
    private const string SellerFolder = "seller";

    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        if (app.Environment.WebRootPath is { } root && Directory.Exists(root))
        {
            var seller = SellerFiles(root);
            app.UseWhen(ctx => IsSellerHost(app, ctx.Request), branch =>
            {
                branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = seller });
                branch.UseStaticFiles(new StaticFileOptions { FileProvider = seller });
            });
            app.UseWhen(ctx => !IsSellerHost(app, ctx.Request), branch =>
            {
                branch.UseDefaultFiles();
                branch.UseStaticFiles();
            });
        }

        return app;
    }

    public static WebApplication MapSpaFallbacks(this WebApplication app)
    {
        if (app.Environment.WebRootPath is { } root && Directory.Exists(root))
        {
            // Host-matched endpoints win over the host-less storefront fallback.
            app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = SellerFiles(root) })
                .RequireHost(SellerHost(app));
            app.MapFallbackToFile("index.html");
        }

        return app;
    }

    private static PhysicalFileProvider SellerFiles(string root) => new(Path.Combine(root, SellerFolder));

    private static string SellerHost(WebApplication app) =>
        new Uri(Seller(app).Origin).Authority;

    private static bool IsSellerHost(WebApplication app, HttpRequest request) =>
        app.Services.GetRequiredService<IOptions<GatewayAuthOptions>>().Value.SurfaceFor(request) == Seller(app);

    private static BffSurface Seller(WebApplication app) =>
        app.Services.GetRequiredService<IOptions<GatewayAuthOptions>>().Value.Seller;
}
