using Microsoft.Extensions.Options;

namespace Gateway.Auth;

/// <summary>
/// Discovery documents name Identity by its public address (the gateway itself). Server-to-server calls made by the
/// OIDC client and the token validator are sent straight to Identity instead of back through this gateway (D-7).
/// </summary>
internal sealed class IdentityBackchannelHandler(IOptions<GatewayAuthOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (request.RequestUri is { IsAbsoluteUri: true } uri
            && string.Equals(uri.GetLeftPart(UriPartial.Authority), settings.PublicOrigin, StringComparison.OrdinalIgnoreCase))
        {
            request.RequestUri = new Uri(settings.InternalOrigin + uri.PathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
