using System.Collections.Immutable;
using System.Security.Claims;
using Identity.Api.Endpoints;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Oidc;

/// <summary>OpenIddict pass-through handlers (design §3.2). Token and revoke bookkeeping stays inside OpenIddict.</summary>
public static class ConnectEndpoints
{
    /// <summary>The <c>aud</c> of every access token; the gateway checks it (design §3.4).</summary>
    public const string ApiAudience = "hivespace-api";

    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post],
            (HttpContext http, IUserRepository users, TimeProvider clock) => AuthorizeAsync(http, users, clock));
        app.MapPost("/connect/token", (HttpContext http, IUserRepository users) => ExchangeAsync(http, users));
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post],
            (Delegate)LogoutAsync);
        return app;
    }

    private static async Task<IResult> AuthorizeAsync(HttpContext http, IUserRepository users, TimeProvider clock)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var session = await http.AuthenticateAsync(UsersEndpoints.CookieScheme);
        var userId = session.Succeeded ? session.Principal?.FindFirstValue(Claims.Subject) : null;
        if (userId is null || !Guid.TryParse(userId, out var id) || await users.FindByIdAsync(id, http.RequestAborted) is not { } user
            || !await PasswordStampMatchesAsync(session.Principal!, users, id, http.RequestAborted)
            || !IsRecentEnough(session.Principal!, request.MaxAge, clock.GetUtcNow()))
        {
            if (session.Succeeded)
            {
                await http.SignOutAsync(UsersEndpoints.CookieScheme);
            }

            // No Identity session: the storefront sign-in screen takes over and comes back here (D-1).
            var back = http.Request.PathBase + http.Request.Path + http.Request.QueryString;
            return Results.Redirect("/signin?returnUrl=" + Uri.EscapeDataString(back));
        }

        return Results.SignIn(
            BuildPrincipal(user, request.GetScopes(), UsersEndpoints.AuthTimeOf(session.Principal!), session.Principal!.FindAll(UsersEndpoints.AmrClaim).Select(c => c.Value)),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// OIDC <c>max_age</c> (Core §3.1.2.1): without the parameter any session passes; with it the interactive sign-in must be at most
    /// that many seconds old. A cookie without <c>auth_time</c> (issued before FEAT-005) counts as not recent (FEAT-005 §3.6).
    /// </summary>
    private static bool IsRecentEnough(ClaimsPrincipal cookie, long? maxAgeSeconds, DateTimeOffset now) =>
        maxAgeSeconds is not { } maxAge || (UsersEndpoints.AuthTimeOf(cookie) is { } authTime && now.ToUnixTimeSeconds() - authTime <= maxAge);

    /// <summary>
    /// The cookie's <c>pwd_at</c> must equal the credential's current stamp exactly (FEAT-003 D-4, FEAT-004 D-5): no stamp for an
    /// account without a password, the current stamp for one with a password. A cookie issued before the password was removed or
    /// changed no longer signs in.
    /// </summary>
    private static async Task<bool> PasswordStampMatchesAsync(ClaimsPrincipal cookie, IUserRepository users, Guid userId, CancellationToken ct) =>
        cookie.FindFirstValue(UsersEndpoints.PasswordStampClaim)
        == (await users.FindCredentialAsync(userId, ct).ConfigureAwait(false))?.Stamp.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<IResult> ExchangeAsync(HttpContext http, IUserRepository users)
    {
        var request = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Fail(Errors.UnsupportedGrantType, "The specified grant type is not supported.");
        }

        var result = await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = result.Principal?.GetClaim(Claims.Subject);

        // Claims come from the database on every exchange, so a verified email shows up on the next token (AC-9).
        if (!Guid.TryParse(subject, out var id) || await users.FindByIdAsync(id, http.RequestAborted) is not { } user)
        {
            return Fail(Errors.InvalidGrant, "The user no longer exists.");
        }

        return Results.SignIn(
            BuildPrincipal(user, result.Principal!.GetScopes(), UsersEndpoints.AuthTimeOf(result.Principal!), result.Principal!.GetClaims(UsersEndpoints.AmrClaim)),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        // Clears hs.idp so the next /bff/login asks for credentials again (D-4, AC-15).
        await http.SignOutAsync(UsersEndpoints.CookieScheme);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IResult Fail(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    /// <summary>Claims in the access and identity tokens (design §3.2): sub, email, email_verified, name, role, lang, auth_time, amr.</summary>
    internal static ClaimsPrincipal BuildPrincipal(User user, IEnumerable<string> scopes, long? authTime = null, IEnumerable<string>? methods = null)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(Claims.Email, user.EmailAddress));
        identity.AddClaim(new Claim("email_verified", user.IsEmailVerified ? "true" : "false", ClaimValueTypes.String));
        identity.AddClaim(new Claim(Claims.Name, user.DisplayName));
        identity.AddClaim(new Claim("lang", user.Language));
        foreach (var role in user.Roles)
        {
            identity.AddClaim(new Claim(Claims.Role, role.Role.ToString()));
        }

        // How and when the user last signed in interactively, for the re-authentication check (FEAT-005 §3.6, D-5).
        if (authTime is { } at)
        {
            identity.AddClaim(new Claim(UsersEndpoints.AuthTimeClaim, at.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        }

        foreach (var method in methods ?? [])
        {
            identity.AddClaim(new Claim(UsersEndpoints.AmrClaim, method));
        }

        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(Destinations.AccessToken, Destinations.IdentityToken);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes.ToImmutableArray());
        principal.SetResources(ApiAudience);
        return principal;
    }
}
