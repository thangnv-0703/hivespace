using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-005 T6, AC-12: <c>max_age</c> at authorize, and <c>auth_time</c> and <c>amr</c> in the tokens, also after a refresh.</summary>
public sealed class ReauthOidcTests : IAsyncLifetime
{
    private const string Redirect = IdentityApiFactory.Issuer + "/bff/callback";

    private IdentityApiFactory _factory = null!;
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory();
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Ac12_max_age_sends_an_older_sign_in_back_to_signin_and_clears_the_cookie()
    {
        using var browser = await SignedInAsync();
        _clock.Advance(TimeSpan.FromSeconds(120));

        using var response = await browser.Client.GetAsync(AuthorizeUrl(Pkce().Challenge, maxAge: 60), Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/signin?returnUrl=", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("max_age%3D60", response.Headers.Location.OriginalString, StringComparison.Ordinal);
        Assert.Contains("hs.idp=;", string.Join('\n', response.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac12_a_sign_in_within_max_age_issues_a_code_and_without_max_age_any_age_does()
    {
        using var browser = await SignedInAsync();
        _clock.Advance(TimeSpan.FromSeconds(30));

        using var fresh = await browser.Client.GetAsync(AuthorizeUrl(Pkce().Challenge, maxAge: 60), Ct);
        _clock.Advance(TimeSpan.FromHours(5));
        using var old = await browser.Client.GetAsync(AuthorizeUrl(Pkce().Challenge), Ct);

        Assert.Contains("code=", fresh.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Contains("code=", old.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac12_access_and_id_tokens_carry_auth_time_and_amr_also_after_a_refresh()
    {
        var signedInAt = _clock.GetUtcNow().ToUnixTimeSeconds();
        using var browser = await SignedInAsync();
        _clock.Advance(TimeSpan.FromMinutes(3));
        var (verifier, challenge) = Pkce();
        using var authorize = await browser.Client.GetAsync(AuthorizeUrl(challenge), Ct);
        var code = System.Web.HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["code"]!;

        var first = await TokenAsync(browser.Client, new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = Redirect,
        });
        var refreshed = await TokenAsync(browser.Client, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = first.GetProperty("refresh_token").GetString()!,
        });

        foreach (var tokens in new[] { first, refreshed })
        {
            foreach (var name in new[] { "access_token", "id_token" })
            {
                var claims = Payload(tokens.GetProperty(name).GetString()!);
                Assert.Equal(signedInAt, claims.GetProperty("auth_time").GetInt64(), tolerance: 5);
                Assert.Equal(["pwd"], Amr(claims));
            }
        }
    }

    private async Task<Browser> SignedInAsync()
    {
        var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<TimeProvider>(_clock)));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return new Browser(client);
    }

    private sealed class Browser(HttpClient client) : IDisposable
    {
        public HttpClient Client => client;

        public void Dispose() => client.Dispose();
    }

    private static async Task<JsonElement> TokenAsync(HttpClient client, Dictionary<string, string> fields)
    {
        fields["client_id"] = "hivespace-storefront";
        fields["client_secret"] = IdentityApiFactory.ClientSecret;
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(fields), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static JsonElement Payload(string jwt) =>
        JsonDocument.Parse(Convert.FromBase64String(Pad(jwt.Split('.')[1].Replace('-', '+').Replace('_', '/')))).RootElement.Clone();

    private static string Pad(string s) => s.PadRight(s.Length + ((4 - (s.Length % 4)) % 4), '=');

    private static string[] Amr(JsonElement claims) =>
        claims.GetProperty("amr") is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : [claims.GetProperty("amr").GetString()!];

    private static string AuthorizeUrl(string challenge, long? maxAge = null) =>
        "/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
        + $"&redirect_uri={Uri.EscapeDataString(Redirect)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
        + $"&code_challenge={challenge}&code_challenge_method=S256"
        + (maxAge is null ? string.Empty : $"&max_age={maxAge}");

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }
}
