using System.Security.Claims;
using Gateway.Auth;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Gateway.Proxy;

/// <summary>Transforms applied to every proxied API call (repo-structure §7, Proxy/).</summary>
internal static class GatewayTransforms
{
    public const string UserIdHeader = "X-User-Id";
    public const string UserRolesHeader = "X-User-Roles";

    public static void Apply(TransformBuilderContext context)
    {
        // Never let a client spoof the identity headers the gateway owns (FR-AUTH-016).
        context.AddRequestHeaderRemove(UserIdHeader);
        context.AddRequestHeaderRemove(UserRolesHeader);
        context.AddRequestHeaderRemove("Authorization");

        context.AddRequestTransform(transform =>
        {
            var http = transform.HttpContext;

            // Correlation ID set by ServiceDefaults middleware travels to the service.
            var correlationId = http.Response.Headers[Extensions.CorrelationHeader].FirstOrDefault();
            if (!string.IsNullOrEmpty(correlationId))
            {
                transform.ProxyRequest.Headers.Remove(Extensions.CorrelationHeader);
                transform.ProxyRequest.Headers.Add(Extensions.CorrelationHeader, correlationId);
            }

            // Token relay (FR-AUTH-015): the validated access token goes downstream as a bearer token; the browser never sees it.
            if (http.Items[SessionAuthenticationHandler.AccessTokenItem] is string accessToken && http.User.Identity?.IsAuthenticated == true)
            {
                transform.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
                transform.ProxyRequest.Headers.TryAddWithoutValidation(UserIdHeader, http.User.FindFirstValue("sub"));
                transform.ProxyRequest.Headers.TryAddWithoutValidation(
                    UserRolesHeader,
                    string.Join(',', http.User.FindAll("role").Select(c => c.Value)));
            }

            // The BFF cookie is for the gateway only; Identity still needs its own hs.idp cookie.
            StripBffCookie(transform.ProxyRequest);

            return ValueTask.CompletedTask;
        });
    }

    private static void StripBffCookie(HttpRequestMessage request)
    {
        if (!request.Headers.TryGetValues("Cookie", out var values))
        {
            return;
        }

        var kept = values
            .SelectMany(v => v.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(c => !c.StartsWith("hs.bff", StringComparison.Ordinal))
            .ToList();
        request.Headers.Remove("Cookie");
        if (kept.Count > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", kept));
        }
    }
}
