using System.Security.Cryptography;
using Identity.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Identity.Api.Oidc;

/// <summary>Settings bound from <c>Identity:*</c> (design §3.2, D-7). Keys are PEM strings so tokens survive restarts.</summary>
public sealed class IdentityOidcOptions
{
    public const string Section = "Identity";

    /// <summary>The public storefront origin on the gateway, because browsers reach <c>/connect/*</c> through it (ADR-0027).</summary>
    public string Issuer { get; set; } = string.Empty;

    public string SigningKeyPem { get; set; } = string.Empty;

    public string EncryptionKeyPem { get; set; } = string.Empty;

    /// <summary>The first-party clients, one per app host, both held by the gateway BFF (ADR-0027).</summary>
    public OidcClientsOptions Clients { get; set; } = new();
}

public sealed class OidcClientsOptions
{
    public const string StorefrontClientId = "hivespace-storefront";
    public const string SellerClientId = "hivespace-seller";

    public OidcClientOptions Storefront { get; set; } = new();

    public OidcClientOptions Seller { get; set; } = new();

    /// <summary>Each client with its configuration key and the gateway callback path on its host (one OIDC scheme per path).</summary>
    public IEnumerable<(string ClientId, string Key, string CallbackPath, OidcClientOptions Options)> All()
    {
        yield return (StorefrontClientId, "Storefront", "/bff/callback", Storefront);
        yield return (SellerClientId, "Seller", "/bff/callback/seller", Seller);
    }
}

public sealed class OidcClientOptions
{
    /// <summary>The app's public origin; its redirect and post-logout URIs are built from it.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

public static class OpenIddictSetup
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public static IServiceCollection AddIdentityOpenIddict(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(IdentityOidcOptions.Section).Get<IdentityOidcOptions>() ?? new IdentityOidcOptions();
        Require(options.Issuer, "Identity:Issuer");
        Require(options.SigningKeyPem, "Identity:SigningKeyPem");
        Require(options.EncryptionKeyPem, "Identity:EncryptionKeyPem");
        foreach (var client in options.Clients.All())
        {
            Require(client.Options.BaseUrl, $"Identity:Clients:{client.Key}:BaseUrl");
            Require(client.Options.ClientSecret, $"Identity:Clients:{client.Key}:ClientSecret");
        }

        services.Configure<IdentityOidcOptions>(configuration.GetSection(IdentityOidcOptions.Section));

        var signing = ECDsa.Create();
        signing.ImportFromPem(options.SigningKeyPem);
        var encryption = RSA.Create();
        encryption.ImportFromPem(options.EncryptionKeyPem);

        services.AddOpenIddict()
            .AddCore(core => core.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(server =>
            {
                server.SetIssuer(new Uri(options.Issuer));
                server.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetRevocationEndpointUris("/connect/revoke")
                    .SetEndSessionEndpointUris("/connect/logout")
                    .SetConfigurationEndpointUris("/.well-known/openid-configuration")
                    .SetJsonWebKeySetEndpointUris("/.well-known/jwks");

                server.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();
                server.RequireProofKeyForCodeExchange();
                server.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess);

                // 15 minute access tokens, 7 day refresh tokens that rotate and never slide (NFR-SEC-004, FR-AUTH-014).
                server.SetAccessTokenLifetime(AccessTokenLifetime)
                    .SetIdentityTokenLifetime(AccessTokenLifetime)
                    .SetRefreshTokenLifetime(RefreshTokenLifetime)
                    .SetRefreshTokenReuseLeeway(TimeSpan.Zero)
                    .DisableSlidingRefreshTokenExpiration();

                server.AddSigningKey(new ECDsaSecurityKey(signing));
                server.AddEncryptionKey(new RsaSecurityKey(encryption));
                // The gateway validates the access token itself with the JWKS, so it must be a plain signed JWT.
                server.DisableAccessTokenEncryption();

                // TLS ends at the gateway; the hop to Identity inside the network is plain HTTP.
                server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(validation =>
            {
                // Identity issued the token, so the server's own keys and issuer validate it: no JWKS fetch (D-5).
                validation.UseLocalServer();
                validation.AddAudiences(ConnectEndpoints.ApiAudience);
                validation.UseAspNetCore();
            });

        services.AddHostedService<GatewayClientSeeder>();
        return services;
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration '{name}' is required (design D-7, ADR-0027).");
        }
    }
}

/// <summary>
/// Registers the first-party clients from configuration, one per app host (ADR-0027): confidential, code + refresh, PKCE.
/// The storefront and seller clients share the Identity session (<c>hs.idp</c>), which is what makes sign-in carry across.
/// </summary>
internal sealed class GatewayClientSeeder(IServiceProvider services, Microsoft.Extensions.Options.IOptions<IdentityOidcOptions> options)
    : IHostedService
{
    /// <summary>The single client used before ADR-0027; removed so it cannot linger in an existing database.</summary>
    private const string RetiredClientId = "hivespace-gateway";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync(RetiredClientId, cancellationToken) is { } retired)
        {
            await manager.DeleteAsync(retired, cancellationToken);
        }

        foreach (var client in options.Value.Clients.All())
        {
            var descriptor = Describe(client.ClientId, client.Key, client.CallbackPath, client.Options);
            var existing = await manager.FindByClientIdAsync(client.ClientId, cancellationToken);
            if (existing is null)
            {
                await manager.CreateAsync(descriptor, cancellationToken);
            }
            else
            {
                await manager.UpdateAsync(existing, descriptor, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static OpenIddictApplicationDescriptor Describe(string clientId, string name, string callbackPath, OidcClientOptions client)
    {
        var baseUrl = client.BaseUrl.TrimEnd('/');
        return new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = client.ClientSecret,
            DisplayName = $"HiveSpace {name.ToLowerInvariant()}",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            RedirectUris = { new Uri(baseUrl + callbackPath) },
            PostLogoutRedirectUris = { new Uri($"{baseUrl}/") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Revocation,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange },
        };
    }
}
