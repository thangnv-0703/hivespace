using System.Security.Cryptography;
using Identity.Infrastructure.Persistence;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Identity.Api.Oidc;

/// <summary>Settings bound from <c>Identity:*</c> (design §3.2, D-7). Keys are PEM strings so tokens survive restarts.</summary>
public sealed class IdentityOidcOptions
{
    public const string Section = "Identity";

    /// <summary>The public gateway origin, because browsers reach <c>/connect/*</c> through the gateway.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Origin that redirect and post-logout URIs are built from.</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string SigningKeyPem { get; set; } = string.Empty;

    public string EncryptionKeyPem { get; set; } = string.Empty;

    public GatewayClientOptions Gateway { get; set; } = new();
}

public sealed class GatewayClientOptions
{
    public const string ClientId = "hivespace-gateway";

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
        Require(options.PublicBaseUrl, "Identity:PublicBaseUrl");
        Require(options.SigningKeyPem, "Identity:SigningKeyPem");
        Require(options.EncryptionKeyPem, "Identity:EncryptionKeyPem");
        Require(options.Gateway.ClientSecret, "Identity:Gateway:ClientSecret");

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
            });

        services.AddHostedService<GatewayClientSeeder>();
        return services;
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration '{name}' is required (design D-7).");
        }
    }
}

/// <summary>Registers the one first-party client, the gateway BFF (confidential, code + refresh, PKCE) from configuration.</summary>
internal sealed class GatewayClientSeeder(IServiceProvider services, Microsoft.Extensions.Options.IOptions<IdentityOidcOptions> options)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var config = options.Value;
        var baseUrl = config.PublicBaseUrl.TrimEnd('/');

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = GatewayClientOptions.ClientId,
            ClientSecret = config.Gateway.ClientSecret,
            DisplayName = "HiveSpace gateway",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            RedirectUris = { new Uri($"{baseUrl}/bff/callback") },
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

        var existing = await manager.FindByClientIdAsync(GatewayClientOptions.ClientId, cancellationToken);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
