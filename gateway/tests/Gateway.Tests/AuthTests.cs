using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gateway.Auth;
using Xunit;

namespace Gateway.Tests;

/// <summary>FEAT-001 AC-12, AC-14..AC-17 and T11's first cross-user and unauthenticated authorization cases.</summary>
public sealed class AuthTests : IClassFixture<GatewayTestHost>
{
    private readonly GatewayTestHost _host;

    public AuthTests(GatewayTestHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HttpRequestMessage Get(string path) => new(HttpMethod.Get, path);

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        using var response = await client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default);
    }

    [Fact]
    public async Task Ac16_protected_route_without_a_session_is_401()
    {
        using var client = _host.NewClient();

        var (status, body) = await SendAsync(client, Get("/v1/order/orders"));

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("common.unauthenticated", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ac16_tampered_wrong_signature_and_expired_tokens_are_401()
    {
        var good = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10));
        var parts = good.Split('.');
        var tampered = $"{parts[0]}.{parts[1][..^2]}AA.{parts[2]}";
        var cases = new[]
        {
            tampered,
            _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), wrongSignature: true),
            _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(-5)),
            "not-a-jwt",
        };

        foreach (var token in cases)
        {
            using var client = _host.NewClient();
            await _host.SignInAsync(client, token, "good-refresh");

            var (status, _) = await SendAsync(client, Get("/v1/order/orders"));

            Assert.Equal(HttpStatusCode.Unauthorized, status);
        }
    }

    [Fact]
    public async Task Ac16_valid_session_relays_the_bearer_token_and_replaces_spoofed_headers()
    {
        var token = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject: "user-42", roles: ["Buyer", "Seller"]);
        using var client = _host.NewClient();
        await _host.SignInAsync(client, token, "good-refresh");
        using var request = Get("/v1/order/orders");
        request.Headers.Add("X-User-Id", "spoofed");
        request.Headers.Add("X-User-Roles", "Admin");
        request.Headers.Add("Authorization", "Bearer spoofed");

        var (status, body) = await SendAsync(client, request);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Bearer " + token, body.GetProperty("authorization").GetString());
        Assert.Equal("user-42", body.GetProperty("userId").GetString());
        Assert.Equal("Buyer,Seller", body.GetProperty("roles").GetString());
        // Downstream services never see the BFF cookie.
        Assert.DoesNotContain("hs.bff", body.GetProperty("cookie").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cross_user_a_session_cannot_act_as_another_user_by_sending_that_users_identity_headers()
    {
        using var alice = _host.NewClient();
        using var bob = _host.NewClient();
        await _host.SignInAsync(alice, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject: "alice"), "good-refresh");
        await _host.SignInAsync(bob, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject: "bob"), "good-refresh");
        using var attempt = Get("/v1/order/orders");
        attempt.Headers.Add("X-User-Id", "bob");

        var (_, asAlice) = await SendAsync(alice, attempt);
        var (_, asBob) = await SendAsync(bob, Get("/v1/order/orders"));

        Assert.Equal("alice", asAlice.GetProperty("userId").GetString());
        Assert.Equal("bob", asBob.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task Ac14_a_token_about_to_expire_is_refreshed_with_the_rotated_refresh_token()
    {
        using var client = _host.NewClient();
        var before = _host.RefreshCalls;
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), "good-refresh", expiresInSeconds: 20);

        var (status, body) = await SendAsync(client, Get("/v1/order/orders"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.StartsWith("Bearer ", body.GetProperty("authorization").GetString(), StringComparison.Ordinal);
        Assert.Equal(before + 1, _host.RefreshCalls);

        // The cookie now holds the new token, so the next request needs no further refresh.
        var (again, _) = await SendAsync(client, Get("/v1/order/orders"));
        Assert.Equal(HttpStatusCode.OK, again);
        Assert.Equal(before + 1, _host.RefreshCalls);
    }

    [Fact]
    public async Task Ac14_a_failed_refresh_clears_the_session_and_answers_401()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), "revoked-refresh", expiresInSeconds: 20);

        var (first, _) = await SendAsync(client, Get("/v1/order/orders"));
        var (second, _) = await SendAsync(client, Get("/v1/order/orders"));

        Assert.Equal(HttpStatusCode.Unauthorized, first);
        Assert.Equal(HttpStatusCode.Unauthorized, second);
    }

    [Fact]
    public async Task Ac17_a_non_get_under_v1_without_the_csrf_header_is_403()
    {
        using var client = _host.NewClient();

        var (status, body) = await SendAsync(client, new HttpRequestMessage(HttpMethod.Post, "/v1/users") { Content = JsonContent.Create(new { }) });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("common.forbidden", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ac17_with_the_csrf_header_the_request_goes_through_on_an_anonymous_route()
    {
        using var client = _host.NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/users") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-HS-CSRF", "1");

        var (status, _) = await SendAsync(client, request);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Theory]
    [InlineData("/cart", "/cart")]
    [InlineData("/connect/authorize?x=1", "/connect/authorize?x=1")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    public void Ac17_return_url_is_honored_only_as_a_local_path(string? input, string expected) =>
        Assert.Equal(expected, LocalPath.OrHome(input));

    [Fact]
    public async Task Bff_login_sends_the_browser_to_the_public_authorize_endpoint_with_pkce()
    {
        using var client = _host.NewClient();

        using var response = await client.GetAsync("/bff/login?returnUrl=/cart", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith(GatewayTestHost.Issuer + "/connect/authorize?", location, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", location, StringComparison.Ordinal);
        Assert.Contains("client_id=hivespace-storefront", location, StringComparison.Ordinal);
        Assert.Contains("offline_access", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/bff/login?returnUrl=/profile/password&reauth=true", true)]
    [InlineData("/bff/login?returnUrl=/profile/password&reauth=false", false)]
    [InlineData("/bff/login?returnUrl=/profile/password", false)]
    public async Task Ac12_bff_login_asks_for_a_fresh_sign_in_with_max_age_60_only_when_reauth_is_true(string url, bool expectsMaxAge)
    {
        using var client = _host.NewClient();

        using var response = await client.GetAsync(url, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.Equal(expectsMaxAge, location.Contains("max_age=60", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bff_login_on_the_seller_host_uses_the_seller_client_and_its_own_callback()
    {
        // ADR-0027: the seller host has its own OIDC client; Identity (and so the authorize endpoint) stays on the storefront host.
        using var client = _host.NewClient(GatewayTestHost.SellerOrigin);

        using var response = await client.GetAsync("/bff/login?returnUrl=/products", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith(GatewayTestHost.Issuer + "/connect/authorize?", location, StringComparison.Ordinal);
        Assert.Contains("client_id=hivespace-seller", location, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString(GatewayTestHost.SellerOrigin + "/bff/callback/seller"), location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_host_is_treated_as_the_storefront()
    {
        using var client = _host.NewClient("http://evil.example");

        using var response = await client.GetAsync("/bff/login", Ct);

        Assert.Contains("client_id=hivespace-storefront", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_host_has_its_own_session_cookie_and_one_host_cannot_use_the_other()
    {
        var token = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10));
        using var raw = _host.Gateway.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(GatewayTestHost.SellerOrigin),
        });
        using var planted = await raw.PostAsync($"/test/session?access={Uri.EscapeDataString(token)}&refresh=good-refresh-hosts&expires=900", null, Ct);
        var sellerCookie = planted.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.StartsWith("hs.bff.sel=", sellerCookie, StringComparison.Ordinal);

        // The seller cookie works on the seller host...
        using var onSeller = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        onSeller.Headers.Add("Cookie", sellerCookie);
        Assert.True((await SendAsync(raw, onSeller)).Body.GetProperty("isAuthenticated").GetBoolean());

        // ...and means nothing on the storefront host, even if a browser sent it there.
        using var onStorefront = new HttpRequestMessage(HttpMethod.Get, GatewayTestHost.Issuer + "/bff/user");
        onStorefront.Headers.Add("Cookie", sellerCookie);
        Assert.False((await SendAsync(raw, onStorefront)).Body.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task Seller_sign_out_uses_the_seller_client_and_returns_to_the_seller_host()
    {
        using var client = _host.NewClient(GatewayTestHost.SellerOrigin);
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), "good-refresh-seller-signout");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith($"{GatewayTestHost.Issuer}/connect/logout?client_id=hivespace-seller", location, StringComparison.Ordinal);
        Assert.Contains("post_logout_redirect_uri=" + Uri.EscapeDataString(GatewayTestHost.SellerOrigin + "/"), location, StringComparison.Ordinal);
        var revoked = _host.RevokedTokens.IndexOf("good-refresh-seller-signout");
        Assert.True(revoked >= 0);
        Assert.Equal("hivespace-seller", _host.RevokingClients[revoked]);
    }

    [Fact]
    public async Task Bff_user_reports_no_session_and_never_returns_tokens()
    {
        using var anonymous = _host.NewClient();
        var (_, none) = await SendAsync(anonymous, Get("/bff/user"));
        Assert.False(none.GetProperty("isAuthenticated").GetBoolean());

        var token = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject: "user-7");
        using var client = _host.NewClient();
        await _host.SignInAsync(client, token, "good-refresh");
        using var response = await client.GetAsync("/bff/user", Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        var user = JsonDocument.Parse(text).RootElement;

        Assert.True(user.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("user-7", user.GetProperty("userId").GetString());
        Assert.Equal("an@example.com", user.GetProperty("email").GetString());
        Assert.Equal("An", user.GetProperty("displayName").GetString());
        Assert.False(user.GetProperty("emailVerified").GetBoolean());
        Assert.Equal("Buyer", user.GetProperty("roles")[0].GetString());
        Assert.DoesNotContain(token, text, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac15_sign_out_revokes_the_refresh_token_clears_the_cookie_and_goes_through_identity_end_session()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), "good-refresh-signout");

        // A cross-site form post is refused.
        using (var cross = new HttpRequestMessage(HttpMethod.Post, "/bff/logout"))
        {
            cross.Headers.Add("Sec-Fetch-Site", "cross-site");
            using var refused = await client.SendAsync(cross, Ct);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/bff/logout");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith($"{GatewayTestHost.Issuer}/connect/logout?client_id=hivespace-storefront", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("good-refresh-signout", _host.RevokedTokens);
        var (status, _) = await SendAsync(client, Get("/v1/order/orders"));
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Ac15_replaying_the_pre_logout_cookie_cannot_refresh_and_is_401()
    {
        // No cookie jar: capture the session cookie exactly as the browser held it before sign-out.
        using var client = _host.Gateway.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        var access = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10));
        var query = $"?access={Uri.EscapeDataString(access)}&refresh=good-refresh-replay&expires=20";
        using var planted = await client.PostAsync("/test/session" + query, null, Ct);
        planted.EnsureSuccessStatusCode();
        var cookie = string.Join("; ", planted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        HttpRequestMessage WithCookie(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Cookie", cookie);
            return request;
        }

        using var logout = WithCookie(HttpMethod.Post, "/bff/logout");
        logout.Headers.Add("Sec-Fetch-Site", "same-origin");
        using var loggedOut = await client.SendAsync(logout, Ct);
        Assert.Equal(HttpStatusCode.Redirect, loggedOut.StatusCode);
        Assert.Contains("good-refresh-replay", _host.RevokedTokens);

        // The old cookie's access token is about to expire, so the gateway must refresh, and the revoked refresh token fails.
        var (status, body) = await SendAsync(client, WithCookie(HttpMethod.Get, "/v1/order/orders"));

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("common.unauthenticated", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_routes_work_without_a_session()
    {
        using var client = _host.NewClient();

        var (status, _) = await SendAsync(client, Get("/v1/categories"));

        Assert.Equal(HttpStatusCode.OK, status);
    }

    // ---- FEAT-002 T6: /v1/me route and POST /bff/session/refresh (AC-7, AC-9) ----

    [Theory]
    [InlineData("GET", "/v1/me/profile")]
    [InlineData("PUT", "/v1/me/profile")]
    [InlineData("PUT", "/v1/me/profile/language")]
    [InlineData("PUT", "/v1/me/password")]
    public async Task Ac9_the_profile_endpoints_without_a_session_are_401_at_the_gateway(string method, string path)
    {
        using var client = _host.NewClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = method == "GET" ? null : JsonContent.Create(new { }) };
        request.Headers.Add("X-HS-CSRF", "1");

        var (status, body) = await SendAsync(client, request);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
        Assert.Equal("common.unauthenticated", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ac9_with_a_session_the_profile_request_is_relayed_with_the_bearer_token_and_a_spoofed_identity_is_replaced()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject: "alice"), UniqueRefresh());
        using var request = Get("/v1/me/profile");
        request.Headers.Add("X-User-Id", "bob");

        var (status, body) = await SendAsync(client, request);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.StartsWith("Bearer ", body.GetProperty("authorization").GetString(), StringComparison.Ordinal);
        Assert.Equal("alice", body.GetProperty("userId").GetString());
    }

    [Theory]
    [InlineData("/v1/me/profile")]
    [InlineData("/v1/me/password")]
    public async Task A_me_write_without_the_csrf_header_is_403(string path)
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), UniqueRefresh());

        var (status, body) = await SendAsync(client, new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new { }) });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("common.forbidden", body.GetProperty("code").GetString());
    }

    /// <summary>The fake Identity accepts any token starting with <c>good-refresh</c>; a fresh one per test keeps the single-flight cache of an earlier test out of the count.</summary>
    private static string UniqueRefresh() => "good-refresh-" + Guid.NewGuid().ToString("N");

    private static HttpRequestMessage Refresh(bool csrf = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/bff/session/refresh");
        if (csrf)
        {
            request.Headers.Add("X-HS-CSRF", "1");
        }

        return request;
    }

    [Fact]
    public async Task Refresh_without_the_csrf_header_is_403_and_does_not_touch_the_tokens()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), UniqueRefresh());
        var before = _host.RefreshCalls;

        var (status, body) = await SendAsync(client, Refresh(csrf: false));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("common.forbidden", body.GetProperty("code").GetString());
        Assert.Equal(before, _host.RefreshCalls);
    }

    [Fact]
    public async Task Ac7_refresh_returns_the_new_name_and_language_at_once_and_rewrites_the_cookie()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), UniqueRefresh());
        var (_, stale) = await SendAsync(client, Get("/bff/user"));
        Assert.Equal(("An", "vi"), (stale.GetProperty("displayName").GetString(), stale.GetProperty("language").GetString()));
        _host.IssuedName = "Lan Tran";
        _host.IssuedLang = "en";
        var before = _host.RefreshCalls;

        try
        {
            var (status, body) = await SendAsync(client, Refresh());
            var (_, after) = await SendAsync(client, Get("/bff/user"));

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Equal(before + 1, _host.RefreshCalls);
            Assert.True(body.GetProperty("isAuthenticated").GetBoolean());
            Assert.Equal(("Lan Tran", "en"), (body.GetProperty("displayName").GetString(), body.GetProperty("language").GetString()));
            // The cookie was rewritten: /bff/user now shows the new values without another exchange.
            Assert.Equal(("Lan Tran", "en"), (after.GetProperty("displayName").GetString(), after.GetProperty("language").GetString()));
            Assert.Equal(before + 1, _host.RefreshCalls);
            Assert.DoesNotContain("rotated-refresh", body.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            _host.IssuedName = "An";
            _host.IssuedLang = "vi";
        }
    }

    [Fact]
    public async Task Two_parallel_refresh_calls_spend_the_refresh_token_once()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), UniqueRefresh());
        var before = _host.RefreshCalls;
        _host.RefreshDelay = TimeSpan.FromMilliseconds(300);

        try
        {
            var results = await Task.WhenAll(SendAsync(client, Refresh()), SendAsync(client, Refresh()));

            Assert.All(results, r => Assert.True(r.Body.GetProperty("isAuthenticated").GetBoolean()));
            Assert.Equal(before + 1, _host.RefreshCalls);
        }
        finally
        {
            _host.RefreshDelay = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Refresh_with_no_session_answers_200_signed_out()
    {
        using var client = _host.NewClient();

        var (status, body) = await SendAsync(client, Refresh());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task A_failed_refresh_clears_the_cookie_and_answers_200_signed_out()
    {
        using var client = _host.NewClient();
        await _host.SignInAsync(client, _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10)), "revoked-refresh");

        var (status, body) = await SendAsync(client, Refresh());
        var (_, after) = await SendAsync(client, Get("/bff/user"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(body.GetProperty("isAuthenticated").GetBoolean());
        Assert.False(after.GetProperty("isAuthenticated").GetBoolean());
    }
}
