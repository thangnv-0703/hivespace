using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FakeIdp;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Infrastructure.Persistence;
using Json.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenIddict.EntityFrameworkCore.Models;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>
/// FEAT-005 T7: PUT /v1/me/password/initial over the real Google flow against the fake IdP, with a fake clock: AC-10, AC-11, AC-15.
/// </summary>
public sealed class SetPasswordApiTests : IAsyncLifetime
{
    private const string Redirect = IdentityApiFactory.Issuer + "/bff/callback";
    private const string NewPassword = "world5678";

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private FakeIdpServer _idp = null!;
    private IdentityApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _idp = await FakeIdpServer.StartAsync(IdentityApiFactory.GoogleClientId, IdentityApiFactory.GoogleClientSecret, IdentityApiFactory.GoogleCallback);
        _factory = new IdentityApiFactory(_idp.Authority, _clock);
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _idp.DisposeAsync();
    }

    [Fact]
    public async Task Ac10_a_recent_google_sign_in_sets_the_first_password()
    {
        using var user = await GoogleOnlyAsync();
        var other = await CodeFlowAsync(user.Browser);
        _clock.Advance(TimeSpan.FromMinutes(5));

        using var response = await PutAsync(user.Browser.Identity, user.Access, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using (var db = Db())
        {
            Assert.True(await db.Credentials.AnyAsync(c => c.UserId == user.UserId, Ct));
            var events = await PasswordChangedEventsAsync(db, user.UserId);
            var data = Assert.Single(events);
            var schema = JsonSchema.FromText(await File.ReadAllTextAsync(SchemaPath(), Ct), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
            Assert.True(schema.Evaluate(data).IsValid, "PasswordChanged data does not match its schema");
            Assert.DoesNotContain(NewPassword, data.GetRawText(), StringComparison.Ordinal);
        }

        // The other authorization is revoked, the current one keeps working.
        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(user.Browser.Identity, other.Refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(user.Browser.Identity, user.Refresh)).StatusCode);

        // The reissued hs.idp (in the browser's jar) signs in silently, and the new password signs in.
        var (verifier, challenge) = Pkce();
        using var authorize = await user.Browser.Identity.GetAsync(AuthorizeUrl(challenge), Ct);
        Assert.StartsWith(Redirect + "?code=", authorize.Headers.Location!.OriginalString, StringComparison.Ordinal);
        using var anonymous = _factory.CreateClient();
        using var signIn = await anonymous.PostAsJsonAsync("/v1/users/sign-in", new { email = user.Email, password = NewPassword }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.NotEmpty(verifier);
    }

    [Fact]
    public async Task Ac19_a_set_password_run_logs_the_user_id_and_never_an_email_password_or_token()
    {
        using var user = await GoogleOnlyAsync();
        // Generated per run so no password literal sits in source (secret scanners).
        var secret = "Pw1-" + Guid.NewGuid().ToString("N");
        using var response = await PutAsync(user.Browser.Identity, user.Access, secret);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var logs = _factory.Logs.Text;
        Assert.Contains($"Password set: user {user.UserId}", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(user.Email, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(user.Access, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac11_without_a_recent_google_sign_in_it_asks_to_reauthenticate_and_writes_nothing()
    {
        using var user = await GoogleOnlyAsync();
        _clock.Advance(TimeSpan.FromMinutes(11));

        using var response = await PutAsync(user.Browser.Identity, user.Access, NewPassword);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "identity.reauthentication_required");
        await AssertNothingWrittenAsync(user.UserId);
    }

    [Fact]
    public async Task Ac11_a_sign_in_exactly_at_ten_minutes_still_counts()
    {
        using var user = await GoogleOnlyAsync();
        _clock.Advance(TimeSpan.FromMinutes(10));

        using var response = await PutAsync(user.Browser.Identity, user.Access, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Ac11_a_password_sign_in_is_not_enough()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var browser = new GoogleBrowser(_factory, _idp);
        using var register = await browser.Identity.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid();
        using var signIn = await browser.Identity.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        var session = await CodeFlowAsync(browser);
        await using (var db = Db())
        {
            await db.Credentials.Where(c => c.UserId == userId).ExecuteDeleteAsync(Ct);
        }

        using var response = await PutAsync(browser.Identity, session.Access, NewPassword);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "identity.reauthentication_required");
    }

    [Fact]
    public async Task Ac11_an_account_that_has_a_password_gets_409_and_nothing_else_is_evaluated()
    {
        using var user = await GoogleOnlyAsync();
        _clock.Advance(TimeSpan.FromMinutes(12));
        await using (var db = Db())
        {
            db.Credentials.Add(Identity.Domain.Credential.Create(user.UserId, "$argon2id$stub", _clock.GetUtcNow()));
            await db.SaveChangesAsync(Ct);
        }

        // Stale re-authentication and a bad password, yet the answer is the 409.
        using var response = await PutAsync(user.Browser.Identity, user.Access, "abc");

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "identity.password_already_set");
    }

    [Fact]
    public async Task Ac11_every_unmet_rule_is_reported_on_newPassword_and_nothing_is_written()
    {
        using var user = await GoogleOnlyAsync();

        using var response = await PutAsync(user.Browser.Identity, user.Access, "abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.Contains("common.validation_failed", body, StringComparison.Ordinal);
        Assert.Contains("password_too_short", body, StringComparison.Ordinal);
        Assert.Contains("password_needs_digit", body, StringComparison.Ordinal);
        await AssertNothingWrittenAsync(user.UserId);
    }

    [Fact]
    public async Task Ac11_without_a_token_it_is_unauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await PutAsync(client, null, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ac11_two_parallel_calls_give_one_204_and_one_409()
    {
        using var user = await GoogleOnlyAsync();

        var statuses = await Task.WhenAll(
            Task.Run(async () => (await PutAsync(user.Browser.Identity, user.Access, NewPassword)).StatusCode, Ct),
            Task.Run(async () => (await PutAsync(user.Browser.Identity, user.Access, "other9999x")).StatusCode, Ct));

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.Conflict], statuses.OrderBy(s => s).ToArray());
        await using var db = Db();
        Assert.Single(await PasswordChangedEventsAsync(db, user.UserId));
    }

    [Fact]
    public async Task Ac15_the_profile_says_whether_the_account_has_a_password()
    {
        using var google = await GoogleOnlyAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        using var password = new GoogleBrowser(_factory, _idp);
        using var register = await password.Identity.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        using var signIn = await password.Identity.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        var session = await CodeFlowAsync(password);

        Assert.False(await HasPasswordAsync(google.Browser.Identity, google.Access));
        Assert.True(await HasPasswordAsync(password.Identity, session.Access));
        using var set = await PutAsync(google.Browser.Identity, google.Access, NewPassword);
        Assert.True(await HasPasswordAsync(google.Browser.Identity, google.Access));
    }

    // ---- helpers ----

    private sealed record Session(string Access, string Refresh);

    private sealed class GoogleUser(GoogleBrowser browser, Guid userId, string email, Session session) : IDisposable
    {
        public GoogleBrowser Browser => browser;

        public Guid UserId => userId;

        public string Email => email;

        public string Access => session.Access;

        public string Refresh => session.Refresh;

        public void Dispose() => browser.Dispose();
    }

    private async Task<GoogleUser> GoogleOnlyAsync()
    {
        var email = $"{Guid.NewGuid():N}@gmail.com";
        _idp.State.Next = new NextSignIn(Outcomes.Ok, "sub-" + Guid.NewGuid().ToString("N"), email, "An");
        var browser = new GoogleBrowser(_factory, _idp);
        var (_, challenge) = Pkce();
        var run = await browser.SignInAsync(AuthorizeUrl(challenge));
        Assert.StartsWith("/connect/authorize", run.FinalLocation, StringComparison.Ordinal);
        var session = await CodeFlowAsync(browser);
        await using var db = Db();
        var userId = (await db.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == email, Ct)).Id;
        return new GoogleUser(browser, userId, email, session);
    }

    private static async Task<Session> CodeFlowAsync(GoogleBrowser browser)
    {
        var (verifier, challenge) = Pkce();
        using var authorize = await browser.Identity.GetAsync(AuthorizeUrl(challenge), Ct);
        var code = System.Web.HttpUtility.ParseQueryString(authorize.Headers.Location!.Query)["code"]
            ?? throw new InvalidOperationException("authorize did not return a code: " + authorize.Headers.Location);
        using var response = await browser.Identity.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = Redirect,
            ["client_id"] = "hivespace-storefront",
            ["client_secret"] = IdentityApiFactory.ClientSecret,
        }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.IsSuccessStatusCode, body);
        var json = JsonDocument.Parse(body).RootElement;
        return new Session(json.GetProperty("access_token").GetString()!, json.GetProperty("refresh_token").GetString()!);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refresh) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = "hivespace-storefront",
            ["client_secret"] = IdentityApiFactory.ClientSecret,
        }), Ct);

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string? bearer, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/v1/me/password/initial") { Content = JsonContent.Create(new { newPassword }) };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<bool> HasPasswordAsync(HttpClient client, string bearer)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.SendAsync(request, Ct);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("hasPassword").GetBoolean();
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    private async Task AssertNothingWrittenAsync(Guid userId)
    {
        await using var db = Db();
        Assert.False(await db.Credentials.AnyAsync(c => c.UserId == userId, Ct));
        Assert.Empty(await PasswordChangedEventsAsync(db, userId));
    }

    private static async Task<List<JsonElement>> PasswordChangedEventsAsync(IdentityDbContext db, Guid userId)
    {
        var rows = await db.Set<OutboxMessage>().Where(m => m.EventType == "PasswordChanged").ToListAsync(Ct);
        return rows.Select(r => EventEnvelope.FromJson(r.Envelope).Data).Where(d => d.GetProperty("userId").GetGuid() == userId).ToList();
    }

    private IdentityDbContext Db() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    private static string AuthorizeUrl(string challenge) =>
        "/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
        + $"&redirect_uri={Uri.EscapeDataString(Redirect)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
        + $"&code_challenge={challenge}&code_challenge_method=S256";

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    private static string SchemaPath([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), "contracts", "events", "PasswordChanged", "v1.schema.json");
    }
}
