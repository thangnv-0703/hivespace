namespace Gateway.Auth;

/// <summary>Settings bound from <c>Identity:*</c> (FEAT-001 design §3.3, D-7; ADR-0027). The same keys configure the Identity service.</summary>
public sealed class GatewayAuthOptions
{
    public const string Section = "Identity";
    public const string ApiAudience = "hivespace-api";

    /// <summary>The public storefront origin, which is also the token issuer (browsers reach /connect/* through the gateway).</summary>
    public string Issuer { get; set; } = "http://localhost:5080";

    /// <summary>Where the gateway reaches Identity server to server (service discovery name in the AppHost).</summary>
    public string InternalBaseUrl { get; set; } = "http://identity";

    public GatewayClients Clients { get; set; } = new();

    public string PublicOrigin => Issuer.TrimEnd('/');

    public string InternalOrigin => InternalBaseUrl.TrimEnd('/');

    /// <summary>The app hosts the gateway serves, storefront first (the fallback for an unknown host).</summary>
    public IReadOnlyList<BffSurface> Surfaces =>
    [
        new("hivespace-storefront", Origin(Clients.Storefront.BaseUrl, PublicOrigin), Clients.Storefront.ClientSecret, "hs.bff.stf", "oidc.stf", "/bff/callback"),
        new("hivespace-seller", Origin(Clients.Seller.BaseUrl, "http://seller.localhost:5080"), Clients.Seller.ClientSecret, "hs.bff.sel", "oidc.sel", "/bff/callback/seller"),
    ];

    public BffSurface Seller => Surfaces[1];

    /// <summary>The surface whose origin has the request's host; the storefront when none does. Never trusts the Host for a redirect target.</summary>
    public BffSurface SurfaceFor(HttpRequest request)
    {
        var surfaces = Surfaces;
        return surfaces.FirstOrDefault(s => string.Equals(new Uri(s.Origin).Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
            ?? surfaces[0];
    }

    private static string Origin(string configured, string fallback) =>
        (string.IsNullOrWhiteSpace(configured) ? fallback : configured).TrimEnd('/');
}

public sealed class GatewayClients
{
    public GatewayClient Storefront { get; set; } = new();

    public GatewayClient Seller { get; set; } = new();
}

public sealed class GatewayClient
{
    /// <summary>The app's public origin. Storefront defaults to the issuer.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>One app host: its OIDC client, its host-only BFF cookie and its OIDC scheme (ADR-0027).</summary>
public sealed record BffSurface(string ClientId, string Origin, string ClientSecret, string CookieScheme, string OidcScheme, string CallbackPath);
