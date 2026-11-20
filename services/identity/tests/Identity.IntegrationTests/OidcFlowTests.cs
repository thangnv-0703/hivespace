using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Design §3.2 / AC-9, AC-12, AC-14, AC-15: code + PKCE, token, refresh with rotation, revoke, claims and lifetimes.</summary>
public sealed class OidcFlowTests : IAsyncLifetime
{
    private const string RedirectUri = IdentityApiFactory.Issuer + "/bff/callback";
    private IdentityApiFactory? _factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory();
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Code_with_pkce_then_refresh_rotation_then_revoke()
    {
        var (client, userId, email) = await SignedInClientAsync();
        var (verifier, challenge) = Pkce();

        var code = await AuthorizeAsync(client, challenge);
        var first = await TokenAsync(client, new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
        });

        // Claims and lifetime of the access token (design §3.2).
        var access = first.GetProperty("access_token").GetString()!;
        var (header, claims) = DecodeJwt(access);
        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.Equal("hivespace-api", claims.GetProperty("aud").GetString());
        Assert.Equal(userId.ToString(), claims.GetProperty("sub").GetString());
        Assert.Equal(email, claims.GetProperty("email").GetString());
        Assert.Equal("false", claims.GetProperty("email_verified").GetString());
        Assert.Equal("An", claims.GetProperty("name").GetString());
        Assert.Equal("Buyer", claims.GetProperty("role").GetString());
        Assert.Equal("vi", claims.GetProperty("lang").GetString());
        Assert.Equal(15 * 60, claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64());
        Assert.Equal(IdentityApiFactory.Issuer, claims.GetProperty("iss").GetString()!.TrimEnd('/'));
        Assert.InRange(first.GetProperty("expires_in").GetInt32(), 899, 900);
        Assert.True(first.TryGetProperty("id_token", out _));

        // Refresh token lifetime is 7 days (recorded by OpenIddict's store).
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var span = await db.Database
                .SqlQuery<double>($"select extract(epoch from (\"ExpirationDate\" - \"CreationDate\")) as \"Value\" from \"OpenIddictTokens\" where \"Type\" like '%refresh_token' limit 1")
                .SingleAsync(Ct);
            Assert.InRange(span, TimeSpan.FromDays(7).TotalSeconds - 5, TimeSpan.FromDays(7).TotalSeconds + 5);
        }

        // The email gets verified; the next token carries email_verified=true (AC-9).
        var token = await VerificationTokenAsync(userId);
        using var verified = await client.PostAsJsonAsync("/v1/users/verify-email", new { token }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);

        var refresh1 = first.GetProperty("refresh_token").GetString()!;
        var second = await TokenAsync(client, new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh1 });
        var (_, claims2) = DecodeJwt(second.GetProperty("access_token").GetString()!);
        Assert.Equal("true", claims2.GetProperty("email_verified").GetString());

        // Rotation: the old refresh token no longer works (AC-14).
        using var replay = await client.PostAsync(
            "/connect/token", Form(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh1 }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_grant", (await replay.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("error").GetString());

        // Revoke the current refresh token, after which it cannot refresh (FR-AUTH-013, AC-15).
        var refresh2 = second.GetProperty("refresh_token").GetString()!;
        using var revoked = await client.PostAsync("/connect/revoke", Form(new() { ["token"] = refresh2 }), Ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var afterRevoke = await client.PostAsync(
            "/connect/token", Form(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh2 }), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Authorize_without_a_session_sends_the_browser_to_the_storefront_sign_in()
    {
        using var client = NewClient();
        var (_, challenge) = Pkce();

        using var response = await client.GetAsync(AuthorizeUrl(challenge), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/signin?returnUrl=", location, StringComparison.Ordinal);
        Assert.StartsWith("/connect/authorize?", Uri.UnescapeDataString(location["/signin?returnUrl=".Length..]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_without_pkce_is_refused()
    {
        var (client, _, _) = await SignedInClientAsync();

        using var response = await client.GetAsync(AuthorizeUrl(null), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_client_secret_is_rejected()
    {
        var (client, _, _) = await SignedInClientAsync();
        var (verifier, challenge) = Pkce();
        var code = await AuthorizeAsync(client, challenge);

        var form = Form(new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["client_secret"] = "wrong",
        }, withSecret: false);
        using var response = await client.PostAsync("/connect/token", form, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_clears_the_identity_session_and_returns_to_the_post_logout_uri()
    {
        var (client, _, _) = await SignedInClientAsync();
        var (_, challenge) = Pkce();
        Assert.Contains("code=", (await client.GetAsync(AuthorizeUrl(challenge), Ct)).Headers.Location!.OriginalString, StringComparison.Ordinal);

        using var logout = await client.GetAsync($"/connect/logout?client_id=hivespace-gateway&post_logout_redirect_uri={Uri.EscapeDataString(IdentityApiFactory.Issuer + "/")}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.StartsWith(IdentityApiFactory.Issuer + "/", logout.Headers.Location!.OriginalString, StringComparison.Ordinal);

        using var again = await client.GetAsync(AuthorizeUrl(Pkce().Challenge), Ct);
        Assert.StartsWith("/signin?returnUrl=", again.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_and_jwks_are_published()
    {
        using var client = NewClient();

        var discovery = await client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", Ct);
        var jwks = await client.GetFromJsonAsync<JsonElement>("/.well-known/jwks", Ct);

        Assert.StartsWith(IdentityApiFactory.Issuer, discovery.GetProperty("issuer").GetString(), StringComparison.Ordinal);
        var key = jwks.GetProperty("keys")[0];
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
    }

    // ---- helpers ----

    private HttpClient NewClient() =>
        _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<(HttpClient Client, Guid UserId, string Email)> SignedInClientAsync()
    {
        var client = NewClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid();
        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return (client, userId, email);
    }

    private static string AuthorizeUrl(string? challenge)
    {
        var url = "/connect/authorize?client_id=hivespace-gateway&response_type=code&state=xyz"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}";
        return challenge is null ? url : url + $"&code_challenge={challenge}&code_challenge_method=S256";
    }

    private static async Task<string> AuthorizeAsync(HttpClient client, string challenge)
    {
        using var response = await client.GetAsync(AuthorizeUrl(challenge), Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(RedirectUri, location.OriginalString, StringComparison.Ordinal);
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("xyz", query["state"]);
        return query["code"]!;
    }

    private static FormUrlEncodedContent Form(Dictionary<string, string> fields, bool withSecret = true)
    {
        fields["client_id"] = "hivespace-gateway";
        if (withSecret)
        {
            fields["client_secret"] = IdentityApiFactory.ClientSecret;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static async Task<JsonElement> TokenAsync(HttpClient client, Dictionary<string, string> fields)
    {
        using var response = await client.PostAsync("/connect/token", Form(fields), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<string> VerificationTokenAsync(Guid userId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        foreach (var row in await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync(Ct))
        {
            var data = EventEnvelope.FromJson(row.Envelope).Data;
            if (data.GetProperty("userId").GetGuid() == userId)
            {
                return data.GetProperty("verificationToken").GetString()!;
            }
        }

        throw new InvalidOperationException("No event for the user.");
    }

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    private static (JsonElement Header, JsonElement Payload) DecodeJwt(string jwt)
    {
        var parts = jwt.Split('.');
        return (Part(parts[0]), Part(parts[1]));

        static JsonElement Part(string segment)
        {
            var padded = segment.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            return JsonDocument.Parse(Convert.FromBase64String(padded)).RootElement.Clone();
        }
    }
}
