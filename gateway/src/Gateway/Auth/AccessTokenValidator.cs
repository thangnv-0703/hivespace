using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Auth;

/// <summary>
/// Checks an access token itself (FR-AUTH-015): ES256 signature against Identity's JWKS, issuer, audience and lifetime.
/// A missing, malformed, expired or wrongly signed token is simply invalid.
/// </summary>
public sealed class AccessTokenValidator
{
    private readonly GatewayAuthOptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenValidator(IOptions<GatewayAuthOptions> options, IHttpClientFactory clients)
    {
        _options = options.Value;
        var http = clients.CreateClient(IdentityBackchannel.ClientName);
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            _options.InternalOrigin + "/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(http) { RequireHttps = false })
        {
            AutomaticRefreshInterval = TimeSpan.FromHours(1),
        };
    }

    public async Task<TokenValidationResult> ValidateAsync(string jwt, CancellationToken cancellationToken)
    {
        var result = await ValidateOnceAsync(jwt, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Identity may have rotated its key since the last fetch.
            _configuration.RequestRefresh();
            result = await ValidateOnceAsync(jwt, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<TokenValidationResult> ValidateOnceAsync(string jwt, CancellationToken cancellationToken)
    {
        OpenIdConnectConfiguration discovery;
        try
        {
            discovery = await _configuration.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return new TokenValidationResult { IsValid = false, Exception = ex };
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuers = [_options.PublicOrigin, _options.PublicOrigin + "/"],
            ValidAudience = GatewayAuthOptions.ApiAudience,
            IssuerSigningKeys = discovery.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(5),
        };
        return await _handler.ValidateTokenAsync(jwt, parameters).ConfigureAwait(false);
    }
}

internal static class IdentityBackchannel
{
    public const string ClientName = "identity-backchannel";
}
