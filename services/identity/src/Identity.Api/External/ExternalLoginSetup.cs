using System.Net;
using System.Security.Claims;
using Identity.Api.Oidc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Identity.Api.External;

/// <summary>Settings bound from <c>Identity:Google:*</c> (design §3.6). Without a client id Google sign-in is off.</summary>
public sealed class GoogleLoginOptions
{
    public const string Section = "Identity:Google";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string Authority { get; set; } = "https://accounts.google.com";
}

public static partial class ExternalLoginSetup
{
    /// <summary>The short-lived cookie that carries the validated Google principal from the callback to <c>complete</c> (D-2).</summary>
    public const string ExternalCookieScheme = "hs.ext";

    /// <summary>The authentication property holding the safe <c>returnUrl</c> through the round trip.</summary>
    public const string ReturnUrlKey = "returnUrl";

    public static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(5);

    /// <summary>After a failed metadata fetch the next try waits this long, so a provider coming back is noticed quickly (design §3.2).</summary>
    public static readonly TimeSpan MetadataRetryInterval = TimeSpan.FromSeconds(30);

    public static string CallbackPath(string provider) => $"/connect/external/{provider}/callback";

    public static string CompletePath(string provider) => $"/connect/external/{provider}/complete";

    /// <summary>The <c>hs.ext</c> cookie and, when <c>Identity:Google:ClientId</c> is set, the <c>google</c> OpenID Connect scheme (D-1).</summary>
    public static AuthenticationBuilder AddExternalLogins(
        this AuthenticationBuilder authentication, IConfiguration configuration, IHostEnvironment environment)
    {
        var secure = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        authentication.AddCookie(ExternalCookieScheme, o =>
        {
            o.Cookie.Name = ExternalCookieScheme;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.Cookie.SecurePolicy = secure;
            o.Cookie.Path = "/connect/external";
            o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            o.SlidingExpiration = false;
        });

        var google = configuration.GetSection(GoogleLoginOptions.Section).Get<GoogleLoginOptions>() ?? new GoogleLoginOptions();
        if (string.IsNullOrWhiteSpace(google.ClientId))
        {
            authentication.Services.AddSingleton(new ExternalLoginProviders([]));
            return authentication;
        }

        if (string.IsNullOrWhiteSpace(google.ClientSecret))
        {
            throw new InvalidOperationException("Configuration 'Identity:Google:ClientSecret' is required when 'Identity:Google:ClientId' is set.");
        }

        var issuer = (configuration[$"{IdentityOidcOptions.Section}:Issuer"] ?? string.Empty).TrimEnd('/');
        var provider = new GoogleLoginProvider();
        authentication.Services.AddSingleton(new ExternalLoginProviders([provider]));
        authentication.AddOpenIdConnect(provider.Scheme, o =>
        {
            o.SignInScheme = ExternalCookieScheme;
            o.Authority = google.Authority;
            o.ClientId = google.ClientId;
            o.ClientSecret = google.ClientSecret;
            o.RequireHttpsMetadata = !IsLoopback(google.Authority);
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.ResponseMode = OpenIdConnectResponseMode.Query;
            o.UsePkce = true;
            o.Scope.Clear();
            o.Scope.Add("openid");
            o.Scope.Add("email");
            o.Scope.Add("profile");
            o.Prompt = "select_account";
            o.CallbackPath = CallbackPath(provider.Name);
            o.MapInboundClaims = false;
            o.SaveTokens = false;
            o.GetClaimsFromUserInfoEndpoint = false;
            o.DisableTelemetry = true;
            o.BackchannelTimeout = ProviderTimeout;
            o.RefreshInterval = MetadataRetryInterval;
            o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(60);
            o.TokenValidationParameters.NameClaimType = "name";
            // The callback is a top-level GET, so Lax cookies come back; Lax also works on plain http://localhost (design §5.2).
            o.CorrelationCookie.SameSite = SameSiteMode.Lax;
            o.CorrelationCookie.SecurePolicy = secure;
            o.NonceCookie.SameSite = SameSiteMode.Lax;
            o.NonceCookie.SecurePolicy = secure;

            o.Events.OnRedirectToIdentityProvider = ctx =>
            {
                // Identity sits behind the gateway, so the request host is internal: use the registered public URI (D-8). The handler
                // stores this value for the code redemption, so both Google calls carry the same redirect_uri.
                ctx.ProtocolMessage.RedirectUri = issuer + CallbackPath(provider.Name);
                return Task.CompletedTask;
            };
            o.Events.OnTokenValidated = ctx =>
            {
                // hs.ext carries the four claims the account decision reads, nothing else (no token, no other claim).
                var claims = new List<Claim>();
                foreach (var type in new[] { "sub", "email", "email_verified", "name" })
                {
                    if (ctx.Principal?.FindFirst(type) is { } claim)
                    {
                        claims.Add(new Claim(type, claim.Value));
                    }
                }

                ctx.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, provider.Scheme, "name", null));
                return Task.CompletedTask;
            };
            o.Events.OnRemoteFailure = ctx =>
            {
                var code = ExternalFailure.Classify(ExternalFailure.ProviderErrorOf(ctx.Failure), ctx.Failure);
                var returnUrl = ExternalLoginEndpoints.SafeReturnUrl(ctx.Properties?.GetString(ReturnUrlKey));
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ExternalLoginSetup));
                LogRemoteFailure(logger, provider.Name, code, ctx.Failure?.GetType().Name ?? "none");
                ctx.Response.Redirect(ExternalFailure.SignInUrl(code, returnUrl));
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });
        return authentication;
    }

    private static bool IsLoopback(string authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip)));

    // Never the code, state, tokens, claims, client secret or email (NFR-SEC-008, AC-16): only names, codes and the exception type.
    [LoggerMessage(Level = LogLevel.Information, Message = "External sign-in with {Provider} failed at the provider step: {Outcome} ({Reason})")]
    private static partial void LogRemoteFailure(ILogger logger, string provider, string outcome, string reason);
}
