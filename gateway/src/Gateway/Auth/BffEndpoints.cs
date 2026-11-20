using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Gateway.Auth;

/// <summary>Endpoints served by the gateway itself (design §3.3, <c>contracts/openapi/gateway.yaml</c>). The browser never receives a token.</summary>
public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bff/login", (string? returnUrl) =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = LocalPath.OrHome(returnUrl) },
                    [GatewayAuth.OidcScheme]))
            .AllowAnonymous();

        app.MapGet("/bff/user", async (HttpContext http, SessionTokens sessions, AccessTokenValidator validator) =>
            {
                var tokens = await sessions.GetFreshAsync(http);
                var validated = tokens is null ? null : await validator.ValidateAsync(tokens.AccessToken, http.RequestAborted);
                if (validated is not { IsValid: true })
                {
                    return Results.Ok(new { isAuthenticated = false });
                }

                var claims = validated.ClaimsIdentity.Claims.ToList();
                string? First(string type) => claims.FirstOrDefault(c => c.Type == type)?.Value;
                return Results.Ok(new
                {
                    isAuthenticated = true,
                    userId = First("sub"),
                    email = First("email"),
                    displayName = First("name"),
                    emailVerified = string.Equals(First("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
                    roles = claims.Where(c => c.Type == "role").Select(c => c.Value).ToArray(),
                    language = First("lang") ?? "vi",
                });
            })
            .AllowAnonymous();

        // A same-origin form POST cannot carry the CSRF header, so the browser's Sec-Fetch-Site decides (D-4).
        app.MapPost("/bff/logout", async (HttpContext http, SessionTokens sessions, Microsoft.Extensions.Options.IOptions<GatewayAuthOptions> options) =>
            {
                if (http.Request.Headers["Sec-Fetch-Site"].ToString() != "same-origin")
                {
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }

                var auth = await http.AuthenticateAsync(SessionTokens.CookieScheme);
                var refresh = auth.Properties?.GetTokenValue("refresh_token");
                var idToken = auth.Properties?.GetTokenValue("id_token");
                if (refresh is not null)
                {
                    await sessions.RevokeAsync(refresh, http.RequestAborted);
                }

                await http.SignOutAsync(SessionTokens.CookieScheme);

                // The browser goes through Identity's end-session so hs.idp is cleared too, then lands on "/".
                var origin = options.Value.PublicOrigin;
                var end = $"{origin}/connect/logout?client_id={GatewayAuthOptions.ClientId}"
                    + $"&post_logout_redirect_uri={Uri.EscapeDataString(origin + "/")}"
                    + (idToken is null ? string.Empty : $"&id_token_hint={Uri.EscapeDataString(idToken)}");
                return Results.Redirect(end);
            })
            .AllowAnonymous();

        return app;
    }
}

public static class LocalPath
{
    /// <summary>A returnUrl is honored only as a local path; anything absolute or protocol-relative becomes "/" (AC-17).</summary>
    public static string OrHome(string? value) =>
        value is { Length: > 0 }
        && value[0] == '/'
        && !value.StartsWith("//", StringComparison.Ordinal)
        && !value.StartsWith("/\\", StringComparison.Ordinal)
        && !value.Contains('\n', StringComparison.Ordinal)
        && !value.Contains('\r', StringComparison.Ordinal)
            ? value
            : "/";
}
