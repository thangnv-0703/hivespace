using System.Security.Claims;
using System.Text.Encodings.Web;
using HiveSpace.BuildingBlocks.Abstractions;
using HiveSpace.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gateway.Auth;

/// <summary>
/// Authenticates a request from the BFF cookie: refreshes the tokens when due, then validates the access token itself
/// (FR-AUTH-015). The raw token goes to <see cref="AccessTokenItem"/> for the relay transform; failures answer 401.
/// </summary>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionTokens sessions,
    AccessTokenValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "hs.session";
    public const string AccessTokenItem = "hs.access_token";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tokens = await sessions.GetFreshAsync(Context).ConfigureAwait(false);
        if (tokens is null)
        {
            return AuthenticateResult.NoResult();
        }

        var result = await validator.ValidateAsync(tokens.AccessToken, Context.RequestAborted).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return AuthenticateResult.Fail("The access token is not valid.");
        }

        Context.Items[AccessTokenItem] = tokens.AccessToken;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(result.ClaimsIdentity.Claims, SchemeName, "name", "role"));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        ApiErrorResponder.WriteAsync(Context, new ApiException(ErrorCodes.Unauthenticated));

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiErrorResponder.WriteAsync(Context, new ApiException(ErrorCodes.Forbidden));
}
