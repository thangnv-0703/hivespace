using System.Security.Cryptography;
using System.Text.Json;
using Gateway.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Gateway.Tests;

/// <summary>
/// The gateway under test with stand-ins for its neighbours: an Identity (discovery, JWKS, token, revoke) and a downstream
/// service that echoes what it received. <c>/test/session</c> plants a BFF cookie without running the browser flow.
/// </summary>
public sealed class GatewayTestHost : IAsyncLifetime
{
    public const string Issuer = "http://localhost:5080";

    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private WebApplication? _identity;
    private WebApplication? _downstream;
    private WebApplicationFactory<Program>? _gateway;

    public List<string> RevokedTokens { get; } = [];

    public int RefreshCalls { get; private set; }

    public WebApplicationFactory<Program> Gateway => _gateway!;

    public async ValueTask InitializeAsync()
    {
        _identity = await StartAsync(app =>
        {
            app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
            {
                issuer = Issuer + "/",
                // Like the real Identity: endpoint URLs carry the host the request came in on, not the public origin.
                authorization_endpoint = "http://identity.internal:1234/connect/authorize",
                token_endpoint = Issuer + "/connect/token",
                jwks_uri = Issuer + "/.well-known/jwks",
                response_types_supported = new[] { "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "ES256" },
            }));
            app.MapGet("/.well-known/jwks", () =>
            {
                var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(new ECDsaSecurityKey(_signingKey));
                jwk.Use = "sig";
                jwk.Alg = "ES256";
                return Results.Text(JsonSerializer.Serialize(new { keys = new[] { jwk } }), "application/json");
            });
            app.MapPost("/connect/token", async (HttpRequest request) =>
            {
                var form = await request.ReadFormAsync();
                RefreshCalls++;
                if (form["grant_type"] != "refresh_token" || !form["refresh_token"].ToString().StartsWith("good-refresh", StringComparison.Ordinal) || form["client_secret"] != "test-secret"
                    || RevokedTokens.Contains(form["refresh_token"].ToString()))
                {
                    return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
                }

                return Results.Json(new
                {
                    access_token = CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(15), subject: "user-1"),
                    refresh_token = "rotated-refresh",
                    expires_in = 900,
                    token_type = "Bearer",
                });
            });
            app.MapPost("/connect/revoke", async (HttpRequest request) =>
            {
                RevokedTokens.Add((await request.ReadFormAsync())["token"].ToString());
                return Results.Ok();
            });
        });

        _downstream = await StartAsync(app => app.Map("/{**path}", (HttpRequest r) => Results.Json(new
        {
            authorization = r.Headers.Authorization.ToString(),
            userId = r.Headers["X-User-Id"].ToString(),
            roles = r.Headers["X-User-Roles"].ToString(),
            cookie = r.Headers.Cookie.ToString(),
            correlationId = r.Headers["X-Correlation-ID"].ToString(),
        })));

        var identityUrl = Address(_identity);
        var downstreamUrl = Address(_downstream);
        _gateway = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("Identity:Issuer", Issuer);
            b.UseSetting("Identity:InternalBaseUrl", identityUrl);
            b.UseSetting("Identity:Gateway:ClientSecret", "test-secret");
            foreach (var cluster in new[] { "catalog", "order", "identity" })
            {
                b.UseSetting($"ReverseProxy:Clusters:{cluster}:Destinations:d1:Address", downstreamUrl);
            }

            b.ConfigureServices(s => s.AddSingleton<IStartupFilter, TestSessionFilter>());
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_gateway is not null)
        {
            await _gateway.DisposeAsync();
        }

        foreach (var app in new[] { _identity, _downstream })
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
        }
    }

    public HttpClient NewClient() =>
        Gateway.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Plants a BFF session. <paramref name="expiresInSeconds"/> is what the cookie claims, independent of the token's own <c>exp</c>.</summary>
    public async Task SignInAsync(HttpClient client, string accessToken, string refreshToken, int expiresInSeconds = 900)
    {
        var query = $"?access={Uri.EscapeDataString(accessToken)}&refresh={Uri.EscapeDataString(refreshToken)}&expires={expiresInSeconds}";
        using var response = await client.PostAsync("/test/session" + query, null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public string CreateAccessToken(DateTimeOffset expires, string subject = "user-1", bool wrongSignature = false, string[]? roles = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer + "/",
            Audience = "hivespace-api",
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            IssuedAt = DateTime.UtcNow.AddMinutes(-20),
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["email"] = "an@example.com",
                ["email_verified"] = "false",
                ["name"] = "An",
                ["lang"] = "vi",
                ["role"] = roles ?? ["Buyer"],
            },
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(wrongSignature ? _otherKey : _signingKey), SecurityAlgorithms.EcdsaSha256),
        });

    private static async Task<WebApplication> StartAsync(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app;
    }

    private static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

    private sealed class TestSessionFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/test/session", branch => branch.Run(async context =>
            {
                // Map() sets PathBase to the branch path, which would become the cookie's Path.
                context.Request.PathBase = PathString.Empty;
                var query = context.Request.Query;
                var props = new AuthenticationProperties();
                props.StoreTokens(SessionTokens.ToAuthTokens(new TokenSet(
                    query["access"]!,
                    query["refresh"]!,
                    null,
                    DateTimeOffset.UtcNow.AddSeconds(int.Parse(query["expires"]!, System.Globalization.CultureInfo.InvariantCulture)))));
                var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("test"));
                await context.SignInAsync(SessionTokens.CookieScheme, principal, props);
            }));
            next(app);
        };
    }
}
