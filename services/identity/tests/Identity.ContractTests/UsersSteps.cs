using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Xunit;

namespace Identity.ContractTests;

/// <summary>Steps for contracts/features/identity/*.feature, driven over HTTP against the real API.</summary>
[Binding]
public sealed class UsersSteps
{
    private static IdentityApiFactory? _factory;

    private HttpResponseMessage? _response;
    private JsonElement _body;
    private string? _token;
    private string? _earlierToken;
    private string _email = "an@example.com";
    private int _verificationEvents;

    [BeforeTestRun]
    public static async Task StartApi()
    {
        _factory = new IdentityApiFactory();
        await _factory.StartAsync();
    }

    [AfterTestRun]
    public static async Task StopApi()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [BeforeScenario]
    public static async Task ResetData()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync("truncate table users, outbox cascade");
    }

    // ---- Given ----

    [Given("a registered user with email {string}")]
    public async Task GivenARegisteredUserWithEmail(string email) => await Register(email, "hello1234");

    [Given("a registered user with email {string} and password {string}")]
    public async Task GivenARegisteredUserWithEmailAndPassword(string email, string password) => await Register(email, password);

    [Given("a registered user with a pending verification token")]
    public async Task GivenAPendingToken() => await Register("an@example.com", "hello1234");

    [Given("a registered user with an expired verification token")]
    public async Task GivenAnExpiredToken()
    {
        await Register("an@example.com", "hello1234");
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync("update verification_tokens set expires_at = now() - interval '1 hour'");
    }

    [Given("a registered user whose email is verified with a token")]
    public async Task GivenAVerifiedUser()
    {
        await Register("an@example.com", "hello1234");
        using var client = _factory!.CreateClient();
        using var verified = await client.PostAsJsonAsync("/v1/users/verify-email", new { token = _token });
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
    }

    // ---- When ----

    [When("a visitor registers with email {string}, name {string} and password {string}")]
    public async Task WhenAVisitorRegisters(string email, string name, string password) =>
        await Post("/v1/users", new { email, displayName = name, password }, null);

    [When("a visitor registers in language {string} with email {string}, name {string} and password {string}")]
    public async Task WhenAVisitorRegistersInLanguage(string language, string email, string name, string password) =>
        await Post("/v1/users", new { email, displayName = name, password }, language);

    [When("the visitor verifies with that token")]
    public async Task WhenTheVisitorVerifiesWithThatToken() => await Post("/v1/users/verify-email", new { token = _token }, null);

    [When("the visitor verifies with token {string}")]
    public async Task WhenTheVisitorVerifiesWithToken(string token) => await Post("/v1/users/verify-email", new { token }, null);

    [When("the visitor requests a new verification email for that user")]
    public async Task WhenResendForThatUser()
    {
        _earlierToken = _token;
        await Post("/v1/users/verification-email", new { email = _email }, null);
    }

    [When("the visitor requests a new verification email for {string}")]
    public async Task WhenResendFor(string email)
    {
        _verificationEvents = await CountEvents("VerificationEmailRequested");
        await Post("/v1/users/verification-email", new { email }, null);
    }

    [When("the visitor signs in with {string} and {string}")]
    public async Task WhenSignIn(string email, string password) =>
        await Post("/v1/users/sign-in", new { email, password }, null);

    [When("the visitor signs in with {string} and {string} and returnUrl {string}")]
    public async Task WhenSignInWithReturnUrl(string email, string password, string returnUrl) =>
        await Post("/v1/users/sign-in", new { email, password, returnUrl }, null);

    // ---- Then ----

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status) => Assert.Equal((HttpStatusCode)status, _response!.StatusCode);

    [Then("the response contains a user id")]
    public void ThenTheResponseContainsAUserId() => Assert.NotEqual(Guid.Empty, _body.GetProperty("userId").GetGuid());

    [Then("the error code is {string}")]
    public void ThenTheErrorCodeIs(string code) => Assert.Equal(code, _body.GetProperty("code").GetString());

    [Then("the error message is {string}")]
    public void ThenTheErrorMessageIs(string message) => Assert.Equal(message, _body.GetProperty("message").GetString());

    [Then("the details include {string}")]
    public void ThenTheDetailsInclude(string name) =>
        Assert.Contains(
            _body.GetProperty("details").EnumerateArray(),
            d => d.GetProperty("code").GetString() == "identity." + name);

    [Then("the earlier token is reported as expired when used")]
    public async Task ThenTheEarlierTokenIsExpired()
    {
        await Post("/v1/users/verify-email", new { token = _earlierToken }, null);
        Assert.Equal(HttpStatusCode.BadRequest, _response!.StatusCode);
        Assert.Equal("identity.verification_token_expired", _body.GetProperty("code").GetString());
    }

    [Then("no verification email event was queued")]
    public async Task ThenNoVerificationEmailEventWasQueued() =>
        Assert.Equal(_verificationEvents, await CountEvents("VerificationEmailRequested"));

    [Then("the redirect URL is {string}")]
    public void ThenTheRedirectUrlIs(string url) => Assert.Equal(url, _body.GetProperty("redirectUrl").GetString());

    [Then("an Identity session cookie is set")]
    public void ThenAnIdentitySessionCookieIsSet()
    {
        var cookies = _response!.Headers.GetValues("Set-Cookie").ToList();
        var idp = Assert.Single(cookies, c => c.StartsWith("hs.idp=", StringComparison.Ordinal));
        Assert.Contains("httponly", idp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", idp, StringComparison.OrdinalIgnoreCase);
    }

    // ---- helpers ----

    private async Task Register(string email, string password)
    {
        _email = email;
        using var client = _factory!.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var userId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        _token = await TokenFromOutbox(userId);
    }

    private async Task Post(string path, object payload, string? language)
    {
        using var client = _factory!.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(payload) };
        if (language is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }

        _response = await client.SendAsync(request);
        var text = await _response.Content.ReadAsStringAsync();
        _body = text.Length > 0 ? JsonDocument.Parse(text).RootElement.Clone() : default;
    }

    private static async Task<string> TokenFromOutbox(Guid userId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        foreach (var row in await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync())
        {
            var data = EventEnvelope.FromJson(row.Envelope).Data;
            if (data.GetProperty("userId").GetGuid() == userId)
            {
                return data.GetProperty("verificationToken").GetString()!;
            }
        }

        throw new InvalidOperationException("No UserRegistered event for the user.");
    }

    private static async Task<int> CountEvents(string eventType)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.Set<OutboxMessage>().CountAsync(m => m.EventType == eventType);
    }
}
