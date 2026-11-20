using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gateway.Auth;

/// <summary>The tokens held in the encrypted <c>hs.bff</c> cookie. They never leave the gateway.</summary>
public sealed record TokenSet(string AccessToken, string RefreshToken, string? IdToken, DateTimeOffset ExpiresAt);

/// <summary>Reads the BFF session, refreshes tokens before they expire and revokes them on sign-out (FR-AUTH-013, FR-AUTH-014).</summary>
public sealed class SessionTokens(IOptions<GatewayAuthOptions> options, IHttpClientFactory clients, TimeProvider time)
{
    public const string CookieScheme = "hs.bff";
    public static readonly TimeSpan RefreshWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InFlightMemory = TimeSpan.FromSeconds(30);

    // Two requests carrying the same old cookie must not both spend one rotating refresh token.
    private readonly ConcurrentDictionary<string, (Lazy<Task<TokenSet?>> Task, DateTimeOffset At)> _inFlight = new();

    /// <summary>The session's tokens, refreshed when the access token expires in under 60 s. Null when there is no usable session (the cookie is cleared if the refresh failed).</summary>
    public Task<TokenSet?> GetFreshAsync(HttpContext http) => GetAsync(http, force: false);

    /// <summary>
    /// Exchanges the refresh token now, ignoring the 60 s window, so claims that changed in Identity (name, language) show up at once
    /// (technical-design §3.2, D-4). Reuses the single-flight refresh and the cookie rewrite of <see cref="GetFreshAsync"/>.
    /// </summary>
    public Task<TokenSet?> ForceRefreshAsync(HttpContext http) => GetAsync(http, force: true);

    private async Task<TokenSet?> GetAsync(HttpContext http, bool force)
    {
        var auth = await http.AuthenticateAsync(CookieScheme).ConfigureAwait(false);
        if (!auth.Succeeded || auth.Properties is null)
        {
            return null;
        }

        var current = Read(auth.Properties);
        if (current is null)
        {
            return null;
        }

        if (!force && current.ExpiresAt - time.GetUtcNow() > RefreshWindow)
        {
            return current;
        }

        var refreshed = await RefreshOnceAsync(current.RefreshToken, http.RequestAborted).ConfigureAwait(false);
        if (refreshed is null)
        {
            await http.SignOutAsync(CookieScheme).ConfigureAwait(false);
            return null;
        }

        // Keeps IssuedUtc and ExpiresUtc, so the 7 day session stays absolute.
        auth.Properties.StoreTokens(ToAuthTokens(refreshed));
        await http.SignInAsync(CookieScheme, auth.Principal!, auth.Properties).ConfigureAwait(false);
        return refreshed;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            using var form = ClientForm(new() { ["token"] = refreshToken, ["token_type_hint"] = "refresh_token" });
            using var response = await clients.CreateClient(IdentityBackchannel.ClientName)
                .PostAsync(options.Value.InternalOrigin + "/connect/revoke", form, cancellationToken).ConfigureAwait(false);
            _ = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            // Sign-out still clears the cookie; an unreachable Identity cannot be helped here.
        }
    }

    public static IEnumerable<AuthenticationToken> ToAuthTokens(TokenSet tokens)
    {
        yield return new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken };
        yield return new AuthenticationToken { Name = "refresh_token", Value = tokens.RefreshToken };
        if (tokens.IdToken is not null)
        {
            yield return new AuthenticationToken { Name = "id_token", Value = tokens.IdToken };
        }

        yield return new AuthenticationToken
        {
            Name = "expires_at",
            Value = tokens.ExpiresAt.ToString("o", CultureInfo.InvariantCulture),
        };
    }

    private static TokenSet? Read(AuthenticationProperties props)
    {
        var access = props.GetTokenValue("access_token");
        var refresh = props.GetTokenValue("refresh_token");
        if (access is null || refresh is null
            || !DateTimeOffset.TryParse(props.GetTokenValue("expires_at"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
        {
            return null;
        }

        return new TokenSet(access, refresh, props.GetTokenValue("id_token"), expires);
    }

    private Task<TokenSet?> RefreshOnceAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        foreach (var stale in _inFlight.Where(e => now - e.Value.At > InFlightMemory).Select(e => e.Key).ToList())
        {
            _inFlight.TryRemove(stale, out _);
        }

        // Lazy: GetOrAdd may run its factory twice under contention, and a plain task would start a second exchange.
        return _inFlight.GetOrAdd(refreshToken, _ => (new Lazy<Task<TokenSet?>>(() => RefreshAsync(refreshToken, cancellationToken)), now)).Task.Value;
    }

    private async Task<TokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            using var form = ClientForm(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken });
            using var response = await clients.CreateClient(IdentityBackchannel.ClientName)
                .PostAsync(options.Value.InternalOrigin + "/connect/token", form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            var access = body.GetProperty("access_token").GetString();
            var refresh = body.GetProperty("refresh_token").GetString();
            if (access is null || refresh is null)
            {
                return null;
            }

            var expiresIn = body.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900;
            return new TokenSet(
                access,
                refresh,
                body.TryGetProperty("id_token", out var id) ? id.GetString() : null,
                time.GetUtcNow().AddSeconds(expiresIn));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException)
        {
            return null;
        }
    }

    private FormUrlEncodedContent ClientForm(Dictionary<string, string> fields)
    {
        fields["client_id"] = GatewayAuthOptions.ClientId;
        fields["client_secret"] = options.Value.Gateway.ClientSecret;
        return new FormUrlEncodedContent(fields);
    }
}
