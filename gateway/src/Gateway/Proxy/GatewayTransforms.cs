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

            // STUB: replaced by the BFF cookie session and bearer-token relay in FEAT-001 (ADR-0008).
            // Development only, so a deployed gateway never asserts an identity it did not verify.
            if (http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                transform.ProxyRequest.Headers.Add(UserIdHeader, "dev-user");
                transform.ProxyRequest.Headers.Add(UserRolesHeader, "Buyer");
            }

            return ValueTask.CompletedTask;
        });
    }
}
