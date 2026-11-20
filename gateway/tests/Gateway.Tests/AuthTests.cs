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
        Assert.Contains("client_id=hivespace-gateway", location, StringComparison.Ordinal);
        Assert.Contains("offline_access", location, StringComparison.Ordinal);
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
        Assert.StartsWith($"{GatewayTestHost.Issuer}/connect/logout?client_id=hivespace-gateway", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
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
}
