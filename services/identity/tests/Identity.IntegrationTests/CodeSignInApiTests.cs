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
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-006 through HTTP: the response shapes and headers of the code endpoints.</summary>
public sealed class CodeSignInApiTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private IdentityApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory(clock: _clock);
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Ac1_ac3_a_request_answers_201_with_the_challenge_then_429_with_retry_after()
    {
        using var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var now = _clock.GetUtcNow();

        using var first = await RequestAsync(client, email);
        _clock.Advance(TimeSpan.FromSeconds(59));
        using var second = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Null(first.Headers.Location);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(Guid.TryParse(body.GetProperty("challengeId").GetString(), out _));
        Assert.Equal(now.AddMinutes(5), body.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(now.AddSeconds(60), body.GetProperty("resendAvailableAt").GetDateTimeOffset());

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("1", second.Headers.GetValues("Retry-After").Single());
        Assert.Equal("identity.sign_in_code_too_soon", (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ac11_a_malformed_email_answers_400_with_a_detail_on_email()
    {
        using var client = _factory.CreateClient();

        using var response = await RequestAsync(client, "a@");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("common.validation_failed", body.GetProperty("code").GetString());
        Assert.Equal("email", body.GetProperty("details")[0].GetProperty("field").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ac5_ac9_the_correct_code_sets_hs_idp_and_authorize_issues_tokens_with_otp_and_a_verified_email(bool hasPassword)
    {
        var (userId, email) = await RegisterAsync();
        if (!hasPassword)
        {
            await using var db = Db();
            await db.Credentials.Where(c => c.UserId == userId).ExecuteDeleteAsync(Ct);
        }

        using var browser = Browser();
        var challengeId = await IssueAsync(browser, email);
        var (verifier, pkce) = Pkce();
        var authorize = AuthorizeUrl(pkce);

        using var signIn = await SubmitAsync(browser, challengeId, await CodeAsync(userId), authorize);

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(authorize, (await signIn.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("redirectUrl").GetString());
        Assert.Contains(signIn.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hs.idp=", StringComparison.Ordinal));

        using var authorized = await browser.GetAsync(authorize, Ct);
        var location = authorized.Headers.Location!.OriginalString;
        Assert.StartsWith(IdentityApiFactory.Issuer + "/bff/callback?code=", location, StringComparison.Ordinal);
        var code = System.Web.HttpUtility.ParseQueryString(new Uri(location).Query)["code"]!;
        using var token = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = IdentityApiFactory.Issuer + "/bff/callback",
            ["client_id"] = "hivespace-storefront",
            ["client_secret"] = IdentityApiFactory.ClientSecret,
        }), Ct);
        var body = await token.Content.ReadAsStringAsync(Ct);
        Assert.True(token.IsSuccessStatusCode, body);
        var claims = Claims(JsonDocument.Parse(body).RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal("otp", claims.GetProperty("amr").GetString());
        Assert.Equal(_clock.GetUtcNow().ToUnixTimeSeconds(), claims.GetProperty("auth_time").GetInt64());
        Assert.Equal("true", claims.GetProperty("email_verified").ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task Ac5_parallel_correct_submissions_give_exactly_one_200()
    {
        var (userId, email) = await RegisterAsync();
        using var client = _factory.CreateClient();
        var challengeId = await IssueAsync(client, email);
        var code = await CodeAsync(userId);

        var statuses = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            using var response = await SubmitAsync(client, challengeId, code);
            return response.StatusCode;
        }));

        Assert.Single(statuses, s => s == HttpStatusCode.OK);
        Assert.All(statuses.Where(s => s != HttpStatusCode.OK), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    [Fact]
    public async Task Ac8_ten_parallel_wrong_codes_answer_401_or_423_and_never_500()
    {
        var (userId, email) = await RegisterAsync();
        using var client = _factory.CreateClient();
        var challengeId = await IssueAsync(client, email);
        var wrong = await CodeAsync(userId) == "000000" ? "000001" : "000000";

        var statuses = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            using var response = await SubmitAsync(client, challengeId, wrong);
            return response.StatusCode;
        }));

        Assert.All(statuses, s => Assert.True(s is HttpStatusCode.Unauthorized or HttpStatusCode.Locked, s.ToString()));
        Assert.Contains(HttpStatusCode.Locked, statuses);
        await using var db = Db();
        Assert.Equal(5, (await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct)).FailedCount);
    }

    [Fact]
    public async Task Ac10_a_code_run_logs_no_code_hash_or_email()
    {
        var (userId, email) = await RegisterAsync();
        using var client = _factory.CreateClient();
        var challengeId = await IssueAsync(client, email);
        var code = await CodeAsync(userId);
        using (await SubmitAsync(client, challengeId, code == "000000" ? "000001" : "000000"))
        using (await SubmitAsync(client, challengeId, code))
        {
        }

        await using var db = Db();
        var challenge = await db.SignInCodeChallenges.AsNoTracking().SingleAsync(c => c.Id == Guid.Parse(challengeId), Ct);
        var logs = _factory.Logs.Text;
        Assert.Contains($"Sign-in code issued: challenge {challengeId}", logs, StringComparison.Ordinal);
        Assert.Contains($"Signed in with a code: user {userId}", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(code, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(email, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToHexString(challenge.CodeHash), logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(challenge.CodeHash), logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(challenge.EmailHash), logs, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Guid UserId, string Email)> RegisterAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return ((await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid(), email);
    }

    private HttpClient Browser() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task<string> IssueAsync(HttpClient client, string email)
    {
        using var response = await RequestAsync(client, email);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("challengeId").GetString()!;
    }

    private async Task<string> CodeAsync(Guid userId)
    {
        await using var db = Db();
        return (await db.Set<OutboxMessage>().AsNoTracking().Where(m => m.EventType == "SignInCodeRequested").ToListAsync(Ct))
            .Select(m => EventEnvelope.FromJson(m.Envelope).Data)
            .Last(d => d.GetProperty("userId").GetGuid() == userId)
            .GetProperty("code").GetString()!;
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, string challengeId, string code, string? returnUrl = null) =>
        client.PostAsJsonAsync("/v1/users/sign-in/code", new { challengeId, code, returnUrl }, Ct);

    private IdentityDbContext Db() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    private static string AuthorizeUrl(string challenge) =>
        "/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
        + $"&redirect_uri={Uri.EscapeDataString(IdentityApiFactory.Issuer + "/bff/callback")}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
        + $"&code_challenge={challenge}&code_challenge_method=S256";

    private static JsonElement Claims(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '='))).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/v1/users/sign-in/code-challenges", new { email }, Ct);
}
