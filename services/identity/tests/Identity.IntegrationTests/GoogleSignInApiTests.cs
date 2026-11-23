using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FakeIdp;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>
/// FEAT-004 T5: Google sign-in through Identity's browser endpoints against the fake IdP (tools/fake-idp): AC-1, AC-2, AC-3 cookie
/// part, AC-5, AC-6, AC-7, AC-10, AC-16.
/// </summary>
public sealed class GoogleSignInApiTests : IAsyncLifetime
{
    private const string BffCallback = IdentityApiFactory.Issuer + "/bff/callback";
    private FakeIdpServer _idp = null!;
    private IdentityApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _idp = await FakeIdpServer.StartAsync(IdentityApiFactory.GoogleClientId, IdentityApiFactory.GoogleClientSecret, IdentityApiFactory.GoogleCallback);
        _factory = new IdentityApiFactory(_idp.Authority);
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _idp.DisposeAsync();
    }

    [Fact]
    public async Task Ac1_a_new_identity_ends_signed_in_and_authorize_issues_a_code()
    {
        var email = NewGmail();
        var sub = Next("ok", email, "Chi Tran");
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();

        var run = await browser.SignInAsync(authorize);

        Assert.Equal(authorize, run.FinalLocation);
        Assert.Contains(run.Steps[^1].SetCookies, c => c.StartsWith("hs.idp=", StringComparison.Ordinal) && !c.StartsWith("hs.idp=;", StringComparison.Ordinal));
        var code = await browser.GetAsync(authorize);
        Assert.StartsWith(BffCallback + "?code=", code.Location, StringComparison.Ordinal);

        await using var db = Db();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.NormalizedEmail == email, Ct);
        Assert.Equal("Chi Tran", user.DisplayName);
        Assert.True(user.IsEmailVerified);
        Assert.False(await db.Credentials.AnyAsync(c => c.UserId == user.Id, Ct));
        Assert.Equal(sub, (await db.ExternalLogins.SingleAsync(l => l.UserId == user.Id, Ct)).Subject);
        Assert.Equal(1, await CountEventsAsync("ExternalLoginLinked", user.Id));
    }

    [Fact]
    public async Task Ac2_a_verified_password_account_is_linked_and_keeps_its_password()
    {
        var email = NewGmail();
        var userId = await RegisterAsync(email, verified: true);
        Next("ok", email);
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();

        var run = await browser.SignInAsync(authorize);

        Assert.Equal(authorize, run.FinalLocation);
        Assert.StartsWith(BffCallback + "?code=", (await browser.GetAsync(authorize)).Location, StringComparison.Ordinal);
        await using var db = Db();
        Assert.Equal(1, await db.ExternalLogins.CountAsync(l => l.UserId == userId, Ct));
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email, Ct));
        using var client = _factory.CreateClient();
        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Ac3_an_old_password_session_of_an_unverified_account_is_sent_to_signin_after_the_link()
    {
        var email = NewGmail();
        await RegisterAsync(email, verified: false);
        using var oldBrowser = new GoogleBrowser(_factory, _idp);
        using (var signIn = await oldBrowser.Identity.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        }

        Assert.StartsWith(BffCallback + "?code=", (await oldBrowser.GetAsync(AuthorizeUrl())).Location, StringComparison.Ordinal);

        Next("ok", email);
        using var google = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();
        var run = await google.SignInAsync(authorize);
        Assert.Equal(authorize, run.FinalLocation);

        var stale = await oldBrowser.GetAsync(AuthorizeUrl());
        Assert.StartsWith("/signin?returnUrl=", stale.Location, StringComparison.Ordinal);
        Assert.Contains(stale.SetCookies, c => c.StartsWith("hs.idp=;", StringComparison.Ordinal));
        // The Google session itself (no pwd_at, no credential) keeps working.
        Assert.StartsWith(BffCallback + "?code=", (await google.GetAsync(AuthorizeUrl())).Location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac7_both_google_calls_carry_the_exact_parameters_and_nothing_is_kept()
    {
        var email = NewGmail();
        Next("ok", email);
        using var browser = new GoogleBrowser(_factory, _idp);

        var run = await browser.SignInAsync(AuthorizeUrl());

        var toGoogle = new Uri(run.ToGoogle!.Location!);
        Assert.Equal(_idp.Authority + "/authorize", toGoogle.GetLeftPart(UriPartial.Path));
        var authorize = _idp.State.Seen.Authorize!;
        Assert.Equal("code", authorize["response_type"]);
        Assert.Equal("S256", authorize["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(authorize["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(authorize["state"]));
        Assert.False(string.IsNullOrEmpty(authorize["nonce"]));
        Assert.Equal("openid email profile", authorize["scope"]);
        Assert.Equal(IdentityApiFactory.GoogleCallback, authorize["redirect_uri"]);
        Assert.Equal("select_account", authorize["prompt"]);
        Assert.Equal(IdentityApiFactory.GoogleClientId, authorize["client_id"]);
        var token = _idp.State.Seen.Token!;
        Assert.Equal(IdentityApiFactory.GoogleCallback, token["redirect_uri"]);
        Assert.Equal("authorization_code", token["grant_type"]);
        Assert.False(string.IsNullOrEmpty(token["code_verifier"]));

        // hs.ext was written at the callback and cleared at complete; a replayed complete finds nothing (AC-6).
        Assert.Contains(run.Steps.Single(s => s.Url.StartsWith("/connect/external/google/callback", StringComparison.Ordinal)).SetCookies,
            c => c.StartsWith("hs.ext=", StringComparison.Ordinal) && !c.StartsWith("hs.ext=;", StringComparison.Ordinal));
        Assert.Contains(run.Steps[^1].SetCookies, c => c.StartsWith("hs.ext=;", StringComparison.Ordinal));
        var replay = await browser.GetAsync("/connect/external/google/complete");
        Assert.StartsWith("/signin?error=identity.external_login_failed", replay.Location, StringComparison.Ordinal);

        // No Google code or token in any row (the outbox holds the only free text Identity writes).
        var code = Uri.UnescapeDataString(System.Web.HttpUtility.ParseQueryString(new Uri(run.Steps[1].Location!).Query)["code"]!);
        await using var db = Db();
        var outbox = string.Join('\n', await db.Set<OutboxMessage>().Select(m => m.Envelope).ToListAsync(Ct));
        Assert.DoesNotContain(code, outbox, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", outbox, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Outcomes.Unverified, "identity.external_email_unverified")]
    [InlineData(Outcomes.NoEmail, "identity.external_email_unverified")]
    [InlineData(Outcomes.Denied, "identity.external_login_denied")]
    [InlineData(Outcomes.ServerError, "identity.external_login_unavailable")]
    [InlineData(Outcomes.BadSignature, "identity.external_login_failed")]
    [InlineData(Outcomes.WrongIssuer, "identity.external_login_failed")]
    [InlineData(Outcomes.WrongAudience, "identity.external_login_failed")]
    [InlineData(Outcomes.Expired, "identity.external_login_failed")]
    [InlineData(Outcomes.WrongNonce, "identity.external_login_failed")]
    [InlineData(Outcomes.NoSub, "identity.external_login_failed")]
    public async Task Ac5_ac6_each_failure_ends_at_signin_with_its_code_the_return_url_and_no_writes(string outcome, string code)
    {
        var email = NewGmail();
        Next(outcome, email);
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();
        var before = await CountAllAsync();

        var run = await browser.SignInAsync(authorize);

        await AssertFailedAsync(run, code, authorize, before);
    }

    [Fact]
    public async Task Ac6_a_tampered_or_missing_state_fails()
    {
        Next("ok", NewGmail());
        using var browser = new GoogleBrowser(_factory, _idp);
        var before = await CountAllAsync();

        var tampered = await browser.SignInAsync(AuthorizeUrl(), url => url.Replace("state=", "state=x", StringComparison.Ordinal));
        var missing = await browser.SignInAsync(AuthorizeUrl(), url => System.Text.RegularExpressions.Regex.Replace(url, "&?state=[^&]*", string.Empty));

        // Without a readable state there is no returnUrl to keep.
        Assert.Equal("/signin?error=identity.external_login_failed", tampered.FinalLocation);
        Assert.Equal("/signin?error=identity.external_login_failed", missing.FinalLocation);
        Assert.Equal(before, await CountAllAsync());
    }

    [Fact]
    public async Task Ac6_a_code_redeemed_with_another_flows_pkce_verifier_fails()
    {
        Next("ok", NewGmail());
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();
        var first = await browser.CallbackUrlAsync(authorize);
        var second = await browser.CallbackUrlAsync(authorize);
        var before = await CountAllAsync();

        // The second flow's code with the first flow's state (and so its verifier and its correlation cookie).
        var mixed = QueryOf(first)["state"];
        var url = "/connect/external/google/callback?code=" + Uri.EscapeDataString(QueryOf(second)["code"]!) + "&state=" + Uri.EscapeDataString(mixed!);
        var run = await browser.ContinueAsync([await browser.GetAsync(url)]);

        await AssertFailedAsync(run, "identity.external_login_failed", authorize, before);
    }

    [Fact]
    public async Task Ac6_an_account_already_linked_to_another_google_subject_fails()
    {
        var email = NewGmail();
        Next("ok", email);
        using (var first = new GoogleBrowser(_factory, _idp))
        {
            Assert.Equal("/", (await first.SignInAsync(null)).FinalLocation);
        }

        Next("ok", email, sub: "another-subject-" + Guid.NewGuid().ToString("N"));
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();
        var before = await CountAllAsync();

        var run = await browser.SignInAsync(authorize);

        await AssertFailedAsync(run, "identity.external_login_failed", authorize, before);
    }

    [Theory]
    [InlineData("https://evil.example/connect/authorize", "/")]
    [InlineData("/cart", "/")]
    [InlineData(null, "/")]
    public async Task Ac10_only_a_local_authorize_return_url_is_honored(string? returnUrl, string end)
    {
        Next("ok", NewGmail());
        using var browser = new GoogleBrowser(_factory, _idp);

        Assert.Equal(end, (await browser.SignInAsync(returnUrl)).FinalLocation);
    }

    [Fact]
    public async Task Ac10_an_error_redirect_drops_an_invalid_return_url()
    {
        Next(Outcomes.Denied, NewGmail());
        using var browser = new GoogleBrowser(_factory, _idp);

        Assert.Equal("/signin?error=identity.external_login_denied", (await browser.SignInAsync("https://evil.example/connect/authorize")).FinalLocation);
    }

    [Theory]
    [InlineData("/connect/external/facebook/start")]
    [InlineData("/connect/external/facebook/complete")]
    public async Task Ac10_an_unknown_provider_is_not_found(string url)
    {
        using var browser = new GoogleBrowser(_factory, _idp);

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(url)).Status);
    }

    [Fact]
    public async Task Ac7_ac16_no_code_token_secret_or_email_reaches_the_logs()
    {
        var email = NewGmail();
        Next("ok", email);
        using var browser = new GoogleBrowser(_factory, _idp);
        var ok = await browser.SignInAsync(AuthorizeUrl());
        Next(Outcomes.BadSignature, email);
        var failed = await browser.SignInAsync(AuthorizeUrl());

        var logs = _factory.Logs.Text;
        Assert.Contains("External sign-in with google succeeded", logs, StringComparison.Ordinal);
        Assert.Contains("identity.external_login_failed", logs, StringComparison.Ordinal);
        foreach (var run in new[] { ok, failed })
        {
            var callback = QueryOf(run.Steps[1].Location!);
            Assert.DoesNotContain(callback["code"]!, logs, StringComparison.Ordinal);
            Assert.DoesNotContain(callback["state"]!, logs, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(IdentityApiFactory.GoogleClientSecret, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(email, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJ", logs, StringComparison.Ordinal);
    }

    // ---- helpers ----

    private async Task AssertFailedAsync(GoogleBrowser.Run run, string code, string returnUrl, (int Users, int Links, int Events) before)
    {
        Assert.Equal($"/signin?error={code}&returnUrl={Uri.EscapeDataString(returnUrl)}", run.FinalLocation);
        Assert.DoesNotContain(run.SetCookies, c => c.StartsWith("hs.idp=", StringComparison.Ordinal) && !c.StartsWith("hs.idp=;", StringComparison.Ordinal));
        // hs.ext may have been written at the callback; whatever was written is cleared by the end of the run.
        var lastExt = run.SetCookies.LastOrDefault(c => c.StartsWith("hs.ext=", StringComparison.Ordinal));
        Assert.True(lastExt is null || lastExt.StartsWith("hs.ext=;", StringComparison.Ordinal), lastExt);
        Assert.Equal(before, await CountAllAsync());
    }

    private string Next(string outcome, string email, string name = "An Nguyen", string? sub = null)
    {
        sub ??= "sub-" + Guid.NewGuid().ToString("N");
        _idp.State.Next = new NextSignIn(outcome, sub, email, name);
        return sub;
    }

    private static string NewGmail() => $"{Guid.NewGuid():N}@gmail.com";

    private static Dictionary<string, string?> QueryOf(string url)
    {
        var query = url.Contains('?', StringComparison.Ordinal) ? url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..] : string.Empty;
        var parsed = System.Web.HttpUtility.ParseQueryString(query);
        return parsed.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => parsed[k]);
    }

    private static string AuthorizeUrl()
    {
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
            + $"&redirect_uri={Uri.EscapeDataString(BffCallback)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
            + $"&code_challenge={challenge}&code_challenge_method=S256";
    }

    private async Task<Guid> RegisterAsync(string email, bool verified)
    {
        using var client = _factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("userId").GetGuid();
        if (verified)
        {
            await using var db = Db();
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerifiedAt, DateTimeOffset.UtcNow), Ct);
        }

        return userId;
    }

    private IdentityDbContext Db() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    private async Task<(int Users, int Links, int Events)> CountAllAsync()
    {
        await using var db = Db();
        return (await db.Users.CountAsync(Ct), await db.ExternalLogins.CountAsync(Ct), await db.Set<OutboxMessage>().CountAsync(Ct));
    }

    private async Task<int> CountEventsAsync(string eventType, Guid userId)
    {
        await using var db = Db();
        var rows = await db.Set<OutboxMessage>().Where(m => m.EventType == eventType).ToListAsync(Ct);
        return rows.Count(r => EventEnvelope.FromJson(r.Envelope).Data.GetProperty("userId").GetGuid() == userId);
    }
}
