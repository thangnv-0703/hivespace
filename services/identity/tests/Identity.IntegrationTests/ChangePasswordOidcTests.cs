using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>
/// PUT /v1/me/password over the real OpenIddict flows: two code flows (storefront and seller clients) for one user.
/// AC-2, AC-3, AC-7, AC-9 and AC-10 (no password in logs).
/// </summary>
public sealed class ChangePasswordOidcTests : IAsyncLifetime
{
    private const string StorefrontRedirect = IdentityApiFactory.Issuer + "/bff/callback";
    private const string SellerRedirect = IdentityApiFactory.SellerOrigin + "/bff/callback/seller";
    private const string Current = "hello1234";
    private const string Next = "world5678";

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
    public async Task Ac2_ac3_the_other_session_is_cut_off_and_the_current_one_rotates_once()
    {
        var user = await SignInBothAsync();

        using var changed = await ChangeAsync(user.Browser, user.A.Access, Current, Next);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        // B (seller client): its refresh token is revoked.
        var b = await RefreshAsync(user.Browser, user.B.Refresh, "hivespace-seller", IdentityApiFactory.SellerClientSecret);
        Assert.Equal(HttpStatusCode.BadRequest, b.Status);
        Assert.Equal("invalid_grant", b.Body.GetProperty("error").GetString());

        // A: the forced refresh succeeds, and the pre-change refresh token is spent afterwards.
        var a = await RefreshAsync(user.Browser, user.A.Refresh, "hivespace-storefront", IdentityApiFactory.ClientSecret);
        Assert.Equal(HttpStatusCode.OK, a.Status);

        // A keeps working across a further refresh (its authorization survived the change).
        var again = await RefreshAsync(user.Browser, a.Body.GetProperty("refresh_token").GetString()!, "hivespace-storefront", IdentityApiFactory.ClientSecret);
        Assert.Equal(HttpStatusCode.OK, again.Status);

        // The pre-change refresh token is spent: no token issued before the change can be redeemed again.
        var replay = await RefreshAsync(user.Browser, user.A.Refresh, "hivespace-storefront", IdentityApiFactory.ClientSecret);
        Assert.Equal(HttpStatusCode.BadRequest, replay.Status);
        Assert.Equal("invalid_grant", replay.Body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Ac7_an_old_session_cookie_no_longer_signs_in_and_the_reissued_one_does()
    {
        var user = await SignInBothAsync();
        using var changed = await ChangeAsync(user.Browser, user.A.Access, Current, Next);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var reissued = IdpCookie(changed);
        Assert.NotNull(reissued);
        Assert.NotEqual(user.IdpCookie, reissued);

        using var other = NewClient(cookies: false);
        var oldTry = await AuthorizeWithCookieAsync(other, user.IdpCookie);
        Assert.StartsWith("/signin?returnUrl=", oldTry.Location, StringComparison.Ordinal);
        Assert.Contains("hs.idp=;", oldTry.SetCookie, StringComparison.Ordinal); // the stale cookie is cleared

        var newTry = await AuthorizeWithCookieAsync(other, reissued);
        Assert.Contains("code=", newTry.Location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac9_no_token_or_a_tampered_token_is_unauthorized_and_a_spoofed_user_id_changes_nothing()
    {
        var victim = await SignInBothAsync();
        var attacker = await SignInBothAsync();

        using var anonymous = NewClient(cookies: false);
        using var none = await ChangeAsync(anonymous, null, Current, Next);
        using var tampered = await ChangeAsync(anonymous, victim.A.Access[..^1] + (victim.A.Access.EndsWith('A') ? 'B' : 'A'), Current, Next);
        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tampered.StatusCode);

        // The attacker's own token with the victim's id in X-User-Id and the body: only the attacker's password is checked.
        using var request = new HttpRequestMessage(HttpMethod.Put, "/v1/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = Current, newPassword = Next, userId = victim.UserId }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attacker.A.Access);
        request.Headers.Add("X-User-Id", victim.UserId.ToString());
        using var spoofed = await anonymous.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.NoContent, spoofed.StatusCode);

        // The victim's password is untouched, the attacker's changed.
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(NewClient(cookies: false), victim.Email, Current)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(NewClient(cookies: false), attacker.Email, Current)).StatusCode);
    }

    [Fact]
    public async Task Ac10_neither_password_appears_in_the_logs()
    {
        var logs = new ConcurrentQueue<string>();
        var logged = _factory!.WithWebHostBuilder(b => b.ConfigureLogging(l =>
        {
            l.SetMinimumLevel(LogLevel.Trace);
            l.AddProvider(new CaptureProvider(logs));
        }));

        var client = logged.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = Current }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = Current }, Ct);
        var (verifier, challenge) = Pkce();
        using var authorize = await client.GetAsync(AuthorizeUrl(challenge, "hivespace-storefront", StorefrontRedirect), Ct);
        var code = System.Web.HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["code"]!;
        var token = await ExchangeAsync(client, code, verifier, "hivespace-storefront", IdentityApiFactory.ClientSecret, StorefrontRedirect);
        var access = token.GetProperty("access_token").GetString()!;

        using var wrong = await ChangeAsync(client, access, "wrongPass99", "newSecret77x");
        using var ok = await ChangeAsync(client, access, Current, "uniqueNew4242");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var output = string.Join('\n', logs);
        Assert.NotEmpty(logs);
        foreach (var secret in new[] { Current, "wrongPass99", "newSecret77x", "uniqueNew4242" })
        {
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }
    }

    // ---- helpers ----

    private sealed record Session(string Access, string Refresh);

    private sealed record Both(HttpClient Browser, Guid UserId, string Email, string IdpCookie, Session A, Session B);

    private HttpClient NewClient(bool cookies = true) =>
        _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = cookies });

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password }, Ct);

    private async Task<Both> SignInBothAsync()
    {
        var browser = NewClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await browser.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = Current }, Ct);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid();
        using var signIn = await SignInAsync(browser, email, Current);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var cookie = IdpCookie(signIn)!;

        var a = await CodeFlowAsync(browser, "hivespace-storefront", IdentityApiFactory.ClientSecret, StorefrontRedirect);
        var b = await CodeFlowAsync(browser, "hivespace-seller", IdentityApiFactory.SellerClientSecret, SellerRedirect);
        return new Both(browser, userId, email, cookie, a, b);
    }

    private static async Task<Session> CodeFlowAsync(HttpClient client, string clientId, string secret, string redirect)
    {
        var (verifier, challenge) = Pkce();
        using var authorize = await client.GetAsync(AuthorizeUrl(challenge, clientId, redirect), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var code = System.Web.HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["code"]!;
        var token = await ExchangeAsync(client, code, verifier, clientId, secret, redirect);
        return new Session(token.GetProperty("access_token").GetString()!, token.GetProperty("refresh_token").GetString()!);
    }

    private static async Task<JsonElement> ExchangeAsync(HttpClient client, string code, string verifier, string clientId, string secret, string redirect)
    {
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> RefreshAsync(HttpClient client, string refresh, string clientId, string secret)
    {
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }), Ct);
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone());
    }

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, string? bearer, string current, string next)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/v1/me/password")
        {
            Content = JsonContent.Create(new { currentPassword = current, newPassword = next }),
        };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request, Ct);
    }

    private static string? IdpCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("hs.idp=", StringComparison.Ordinal))?.Split(';')[0]
            : null;

    private static async Task<(string Location, string SetCookie)> AuthorizeWithCookieAsync(HttpClient client, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthorizeUrl(Pkce().Challenge, "hivespace-storefront", StorefrontRedirect));
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var setCookie = response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join('\n', values) : string.Empty;
        return (response.Headers.Location!.OriginalString, setCookie);
    }

    private static string AuthorizeUrl(string challenge, string clientId, string redirectUri) =>
        $"/connect/authorize?client_id={clientId}&response_type=code&state=xyz"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
        + $"&code_challenge={challenge}&code_challenge_method=S256";

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    private sealed class CaptureProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capture(sink, categoryName);

        public void Dispose()
        {
        }

        private sealed class Capture(ConcurrentQueue<string> sink, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                sink.Enqueue($"{category}: {formatter(state, exception)} {exception}");
        }
    }
}
