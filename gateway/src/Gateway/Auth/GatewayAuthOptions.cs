namespace Gateway.Auth;

/// <summary>Settings bound from <c>Identity:*</c> (FEAT-001 design §3.3, D-7). The same keys configure the Identity service.</summary>
public sealed class GatewayAuthOptions
{
    public const string Section = "Identity";
    public const string ClientId = "hivespace-gateway";
    public const string ApiAudience = "hivespace-api";

    /// <summary>The public gateway origin, which is also the token issuer (browsers reach /connect/* through the gateway).</summary>
    public string Issuer { get; set; } = "http://localhost:5080";

    /// <summary>Where the gateway reaches Identity server to server (service discovery name in the AppHost).</summary>
    public string InternalBaseUrl { get; set; } = "http://identity";

    public GatewayClientSecret Gateway { get; set; } = new();

    public string PublicOrigin => Issuer.TrimEnd('/');

    public string InternalOrigin => InternalBaseUrl.TrimEnd('/');
}

public sealed class GatewayClientSecret
{
    public string ClientSecret { get; set; } = string.Empty;
}
