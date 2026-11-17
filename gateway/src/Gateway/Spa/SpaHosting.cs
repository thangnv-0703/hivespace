namespace Gateway.Spa;

/// <summary>
/// Static hosting for the two SPAs from one origin (architecture §5.2): storefront at "/", seller centre at "/seller".
/// Only active when wwwroot exists (the production image); in development YARP forwards to Vite instead.
/// </summary>
internal static class SpaHosting
{
    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        if (app.Environment.WebRootPath is { } root && Directory.Exists(root))
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        return app;
    }

    public static WebApplication MapSpaFallbacks(this WebApplication app)
    {
        if (app.Environment.WebRootPath is { } root && Directory.Exists(root))
        {
            app.MapFallbackToFile("/seller/{**path}", "seller/index.html");
            app.MapFallbackToFile("index.html");
        }

        return app;
    }
}
