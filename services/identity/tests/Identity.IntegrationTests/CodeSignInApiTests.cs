using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/v1/users/sign-in/code-challenges", new { email }, Ct);
}
