using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gateway.Tests;

/// <summary>FEAT-005 T8: the NFR-SEC-005 request limits at the gateway (AC-13, AC-14).</summary>
public sealed class RateLimitTests : IClassFixture<GatewayTestHost>
{
    private readonly GatewayTestHost _host;

    public RateLimitTests(GatewayTestHost host) => _host = host;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A gateway that trusts X-Forwarded-For, so each test can play many client addresses.</summary>
    private WebApplicationFactory<Program> Trusting(bool trust = true) =>
        _host.Gateway.WithWebHostBuilder(b => b.UseSetting("Gateway:TrustForwardedFor", trust ? "true" : "false"));

    private static HttpRequestMessage Post(string path, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-HS-CSRF", "1");
        request.Headers.Add("X-Forwarded-For", ip);
        return request;
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, HttpRequestMessage request)
    {
        using var response = await client.SendAsync(request, Ct);
        return response.StatusCode;
    }

    [Fact]
    public async Task Ac13_the_eleventh_request_from_one_ip_to_the_public_auth_routes_is_rejected_and_another_ip_is_not()
    {
        using var factory = Trusting();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var paths = new[] { "/v1/users/sign-in", "/v1/users", "/v1/users/verification-email", "/v1/users/verify-email", "/v1/users/sign-in" };
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, Post(paths[i % paths.Length], "203.0.113.10")));
        }

        using var rejected = await client.SendAsync(Post("/v1/users/sign-in", "203.0.113.10"), Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(int.Parse(rejected.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) is > 0 and <= 60);
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("common.rate_limited", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, Post("/v1/users/sign-in", "203.0.113.11")));
    }

    [Fact]
    public async Task Ac13_the_eleventh_password_call_from_one_user_is_rejected_and_another_user_is_not()
    {
        using var factory = Trusting();
        using var a = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        using var b = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(a, "user-rate-a");
        await SignInAsync(b, "user-rate-b");

        // Both password endpoints count against the same user, from changing addresses.
        for (var i = 0; i < 10; i++)
        {
            var request = Put(i % 2 == 0 ? "/v1/me/password" : "/v1/me/password/initial", "198.51.100." + i);
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(a, request));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await StatusAsync(a, Put("/v1/me/password", "198.51.100.200")));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(b, Put("/v1/me/password", "198.51.100.200")));
    }

    [Fact]
    public async Task Ac13_the_eleventh_google_callback_from_one_ip_is_redirected_to_signin_with_the_reason()
    {
        using var factory = Trusting();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        for (var i = 0; i < 10; i++)
        {
            var ok = new HttpRequestMessage(HttpMethod.Get, "/connect/external/google/callback?code=x");
            ok.Headers.Add("X-Forwarded-For", "203.0.113.50");
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, ok));
        }

        var eleventh = new HttpRequestMessage(HttpMethod.Get, "/connect/external/google/callback?code=x");
        eleventh.Headers.Add("X-Forwarded-For", "203.0.113.50");
        using var response = await client.SendAsync(eleventh, Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/signin?error=common.rate_limited", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Ac14_with_forwarded_headers_trusted_two_client_addresses_use_two_partitions()
    {
        using var factory = Trusting(trust: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, Post("/v1/users/sign-in", "192.0.2.1")));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await StatusAsync(client, Post("/v1/users/sign-in", "192.0.2.1")));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, Post("/v1/users/sign-in", "192.0.2.2")));
    }

    [Fact]
    public async Task Ac14_with_forwarded_headers_not_trusted_the_header_is_ignored_and_cannot_pick_a_partition()
    {
        using var factory = Trusting(trust: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(client, Post("/v1/users/sign-in", "192.0.2." + i)));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await StatusAsync(client, Post("/v1/users/sign-in", "192.0.2.99")));
    }

    [Fact]
    public async Task The_new_routes_reach_identity_with_the_right_authorization()
    {
        using var factory = Trusting();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var signedIn = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await SignInAsync(signedIn, "user-route");

        // Password routes need a session (authenticated by default)...
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(anonymous, Put("/v1/me/password", "192.0.2.7")));
        Assert.Equal(HttpStatusCode.Unauthorized, await StatusAsync(anonymous, Put("/v1/me/password/initial", "192.0.2.7")));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(signedIn, Put("/v1/me/password/initial", "192.0.2.7")));
        // ...and Google's callback does not.
        var callback = new HttpRequestMessage(HttpMethod.Get, "/connect/external/google/callback?code=x");
        callback.Headers.Add("X-Forwarded-For", "192.0.2.8");
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(anonymous, callback));
    }

    private static HttpRequestMessage Put(string path, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-HS-CSRF", "1");
        request.Headers.Add("X-Forwarded-For", ip);
        return request;
    }

    private async Task SignInAsync(HttpClient client, string subject)
    {
        var access = _host.CreateAccessToken(DateTimeOffset.UtcNow.AddMinutes(10), subject);
        using var response = await client.PostAsync(
            $"/test/session?access={Uri.EscapeDataString(access)}&refresh=good-refresh-rate&expires=900&sub={Uri.EscapeDataString(subject)}", null, Ct);
        response.EnsureSuccessStatusCode();
    }
}
