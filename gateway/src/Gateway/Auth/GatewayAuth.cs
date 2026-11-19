using HiveSpace.BuildingBlocks.Errors;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Auth;

/// <summary>Wires the BFF (design §3.3, §3.4, §5.3): cookie + OIDC client, token validation, route policies, CSRF and the rate-limit hook.</summary>
public static class GatewayAuth
{
    public const string OidcScheme = "oidc";
    public const string AuthenticatedPolicy = "authenticated";
    public const string AuthRateLimitPolicy = "auth";
    public const string CsrfHeader = "X-HS-CSRF";

    public static WebApplicationBuilder AddGatewayAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.Configure<GatewayAuthOptions>(builder.Configuration.GetSection(GatewayAuthOptions.Section));
        var settings = builder.Configuration.GetSection(GatewayAuthOptions.Section).Get<GatewayAuthOptions>() ?? new GatewayAuthOptions();
        var development = builder.Environment.IsDevelopment();

        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient(IdentityBackchannel.ClientName)
            .AddHttpMessageHandler(sp => new IdentityBackchannelHandler(sp.GetRequiredService<IOptions<GatewayAuthOptions>>()));
        services.AddSingleton<SessionTokens>();
        services.AddSingleton<AccessTokenValidator>();

        AddDataProtection(builder);

        services.AddAuthentication(SessionTokens.CookieScheme)
            .AddCookie(SessionTokens.CookieScheme, o =>
            {
                o.Cookie.Name = SessionTokens.CookieScheme;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                // Absolute 7 days, the refresh token's lifetime: no sliding.
                o.ExpireTimeSpan = TimeSpan.FromDays(7);
                o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            })
            .AddOpenIdConnect(OidcScheme, o =>
            {
                o.SignInScheme = SessionTokens.CookieScheme;
                o.Authority = settings.PublicOrigin;
                o.MetadataAddress = settings.InternalOrigin + "/.well-known/openid-configuration";
                o.RequireHttpsMetadata = false;
                o.ClientId = GatewayAuthOptions.ClientId;
                o.ClientSecret = settings.Gateway.ClientSecret;
                o.ResponseType = "code";
                // Query, not form_post: the callback is then a top-level GET, so SameSite=Lax correlation cookies work over plain http in dev.
                o.ResponseMode = "query";
                o.UsePkce = true;
                o.CallbackPath = "/bff/callback";
                o.SaveTokens = true;
                o.GetClaimsFromUserInfoEndpoint = false;
                o.MapInboundClaims = false;
                o.Scope.Clear();
                foreach (var scope in new[] { "openid", "email", "profile", "roles", "offline_access" })
                {
                    o.Scope.Add(scope);
                }

                // Discovery is fetched on Identity's internal address, so its endpoint URLs name the internal host. The browser
                // must go through the gateway, so the authorize redirect is always built on the public origin (D-7).
                o.Events.OnRedirectToIdentityProvider = ctx =>
                {
                    ctx.ProtocolMessage.IssuerAddress = settings.PublicOrigin + "/connect/authorize";
                    return Task.CompletedTask;
                };

                o.TokenValidationParameters.NameClaimType = "name";
                o.TokenValidationParameters.RoleClaimType = "role";
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });

        // Server-to-server calls of the OIDC handler go straight to Identity (D-7).
        services.AddOptions<OpenIdConnectOptions>(OidcScheme).Configure<IHttpClientFactory>((o, clients) =>
            o.Backchannel = clients.CreateClient(IdentityBackchannel.ClientName));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthenticatedPolicy, p => p.AddAuthenticationSchemes(SessionAuthenticationHandler.SchemeName).RequireAuthenticatedUser());

        // Hook only: the real sign-in and resend limits arrive with FEAT-005.
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddFixedWindowLimiter(AuthRateLimitPolicy, w =>
            {
                w.PermitLimit = 1000;
                w.Window = TimeSpan.FromMinutes(1);
                w.QueueLimit = 0;
            });
        });

        // Every route is authenticated unless its configuration says "anonymous" (FR-AUTH-016).
        services.AddSingleton<IProxyConfigFilter, AuthenticatedByDefaultFilter>();
        return builder;
    }

    public static WebApplication UseGatewayAuth(this WebApplication app)
    {
        app.UseMiddleware<CsrfHeaderMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }

    // Keys live in Azurite (blob "dataprotection") so every gateway instance reads the same cookies (design §3.4).
    // Tests and a bare `dotnet run` have no storage and use the default key ring; a deployed gateway always has "blobs".
    private static void AddDataProtection(WebApplicationBuilder builder)
    {
        var protection = builder.Services.AddDataProtection().SetApplicationName("hivespace-gateway");
        if (builder.Configuration.GetConnectionString("blobs") is not { } connectionString)
        {
            return;
        }

        var container = new BlobContainerClient(connectionString, "dataprotection");
        container.CreateIfNotExists();
        protection.PersistKeysToAzureBlobStorage(container.GetBlobClient("keys.xml"));
    }
}

/// <summary>Gives every YARP route without an explicit policy the "authenticated" one.</summary>
internal sealed class AuthenticatedByDefaultFilter : IProxyConfigFilter
{
    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel) => new(cluster);

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel) =>
        new(route.AuthorizationPolicy is null ? route with { AuthorizationPolicy = GatewayAuth.AuthenticatedPolicy } : route);
}

/// <summary>Non-GET requests under <c>/v1/*</c> must carry <c>X-HS-CSRF: 1</c> (D-5, AC-17); the SPA client adds it in one place.</summary>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var safe = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        if (!safe
            && request.Path.StartsWithSegments("/v1", StringComparison.Ordinal)
            && request.Headers[GatewayAuth.CsrfHeader] != "1")
        {
            await ApiErrorResponder.WriteAsync(context, new ApiException(ErrorCodes.Forbidden));
            return;
        }

        await next(context);
    }
}
