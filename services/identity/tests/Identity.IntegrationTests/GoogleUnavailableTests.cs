using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FakeIdp;
using Identity.Api.External;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>
/// FEAT-004 T5, AC-8: Google down at start (discovery), at the token call (503) and refusing connections. Each test has its own app,
/// because the provider metadata is cached per app. Password sign-in keeps working (FR-AUTH-023).
/// </summary>
public sealed class GoogleUnavailableTests : IAsyncLifetime
{
    private const string ReturnUrl = "/connect/authorize?client_id=hivespace-storefront";
    private static readonly string Unavailable = $"/signin?error=identity.external_login_unavailable&returnUrl={Uri.EscapeDataString(ReturnUrl)}";
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
    public async Task Ac8_down_at_start_redirects_to_unavailable_password_sign_in_works_and_google_recovers_within_30_s()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();
        using (var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct))
        {
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        }

        _idp.State.Next = NextSignIn.Default with { Outcome = Outcomes.Down };
        using var browser = new GoogleBrowser(_factory, _idp);
        var clock = Stopwatch.StartNew();

        var start = await browser.GetAsync(GoogleBrowser.StartUrl(ReturnUrl));

        Assert.Equal((HttpStatusCode.Redirect, Unavailable), (start.Status, start.Location));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), $"took {clock.Elapsed}");
        using (var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        }

        // Google is back: the next attempts work within the 30 s metadata retry interval.
        _idp.State.Next = NextSignIn.Default with { Sub = "sub-" + Guid.NewGuid().ToString("N"), Email = $"{Guid.NewGuid():N}@gmail.com" };
        var back = Stopwatch.StartNew();
        GoogleBrowser.Step attempt;
        do
        {
            await Task.Delay(TimeSpan.FromSeconds(1), Ct);
            attempt = await browser.GetAsync(GoogleBrowser.StartUrl(ReturnUrl));
        }
        while (attempt.Location == Unavailable && back.Elapsed < TimeSpan.FromSeconds(40));

        Assert.StartsWith(_idp.Authority + "/authorize", attempt.Location, StringComparison.Ordinal);
        Assert.True(back.Elapsed <= ExternalLoginSetup.MetadataRetryInterval + TimeSpan.FromSeconds(3), $"took {back.Elapsed}");
        Assert.Equal(ReturnUrl, (await browser.SignInAsync(ReturnUrl)).FinalLocation);
    }

    [Fact]
    public async Task Ac8_a_503_at_the_token_call_redirects_to_unavailable()
    {
        using var browser = new GoogleBrowser(_factory, _idp);
        var callback = await browser.CallbackUrlAsync(ReturnUrl);
        _idp.State.Next = NextSignIn.Default with { Outcome = Outcomes.Down };

        var run = await browser.ContinueAsync([await browser.GetAsync(callback)]);

        Assert.Equal(Unavailable, run.FinalLocation);
        Assert.DoesNotContain(run.SetCookies, c => c.StartsWith("hs.idp=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac8_refused_connections_at_the_token_call_redirect_to_unavailable_and_it_works_again_after()
    {
        _idp.State.Next = NextSignIn.Default with { Sub = "sub-" + Guid.NewGuid().ToString("N"), Email = $"{Guid.NewGuid():N}@gmail.com" };
        using var browser = new GoogleBrowser(_factory, _idp);
        var callback = await browser.CallbackUrlAsync(ReturnUrl);
        await _idp.StopAsync();
        var clock = Stopwatch.StartNew();

        var run = await browser.ContinueAsync([await browser.GetAsync(callback)]);

        Assert.Equal(Unavailable, run.FinalLocation);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(6), $"took {clock.Elapsed}");
        await FakeIdpServer.StartAsync(_idp);
        Assert.Equal(ReturnUrl, (await browser.SignInAsync(ReturnUrl)).FinalLocation);
    }

    [Fact]
    public async Task Without_a_client_id_start_answers_unavailable()
    {
        await using var factory = new IdentityApiFactory();
        await factory.StartAsync();
        using var browser = new GoogleBrowser(factory, _idp);

        var start = await browser.GetAsync(GoogleBrowser.StartUrl(ReturnUrl));

        Assert.Equal((HttpStatusCode.Redirect, Unavailable), (start.Status, start.Location));
    }
}

/// <summary><see cref="ExternalFailure.Classify"/>: each branch (design §3.4).</summary>
public sealed class ExternalFailureTests
{
    [Theory]
    [InlineData("access_denied", ExternalFailure.Denied)]
    [InlineData("temporarily_unavailable", ExternalFailure.Unavailable)]
    [InlineData("server_error", ExternalFailure.Unavailable)]
    [InlineData("invalid_grant", ExternalFailure.Failed)]
    [InlineData("invalid_request", ExternalFailure.Failed)]
    public void A_provider_error_decides(string error, string expected) =>
        Assert.Equal(expected, ExternalFailure.Classify(error, new HttpRequestException("ignored when error is set")));

    [Fact]
    public void Network_failures_and_timeouts_are_unavailable()
    {
        Assert.Equal(ExternalFailure.Unavailable, ExternalFailure.Classify(null, new HttpRequestException("refused")));
        Assert.Equal(ExternalFailure.Unavailable, ExternalFailure.Classify(null, new TaskCanceledException("timeout")));
        Assert.Equal(ExternalFailure.Unavailable, ExternalFailure.Classify(null, new TimeoutException()));
        Assert.Equal(ExternalFailure.Unavailable, ExternalFailure.Classify(null, new IOException("IDX20807: metadata 503")));
        Assert.Equal(
            ExternalFailure.Unavailable,
            ExternalFailure.Classify(null, new InvalidOperationException("IDX20803", new IOException("IDX20804", new HttpRequestException()))));
    }

    [Fact]
    public void Validation_failures_and_no_exception_are_failed()
    {
        Assert.Equal(ExternalFailure.Failed, ExternalFailure.Classify(null, new OpenIdConnectProtocolInvalidNonceException("nonce")));
        Assert.Equal(ExternalFailure.Failed, ExternalFailure.Classify(null, new Microsoft.IdentityModel.Tokens.SecurityTokenInvalidSignatureException()));
        Assert.Equal(ExternalFailure.Failed, ExternalFailure.Classify(null, new InvalidOperationException("Correlation failed.")));
        Assert.Equal(ExternalFailure.Failed, ExternalFailure.Classify(null, null));
    }

    [Fact]
    public void The_provider_error_is_read_from_the_exception_data()
    {
        var exception = new OpenIdConnectProtocolException("Message contains error");
        exception.Data["error"] = "access_denied";

        Assert.Equal("access_denied", ExternalFailure.ProviderErrorOf(exception));
        Assert.Null(ExternalFailure.ProviderErrorOf(new InvalidOperationException()));
        Assert.Null(ExternalFailure.ProviderErrorOf(null));
    }

    [Theory]
    [InlineData(null, "/signin?error=identity.external_login_failed")]
    [InlineData("/connect/authorize?a=1&b=2", "/signin?error=identity.external_login_failed&returnUrl=%2Fconnect%2Fauthorize%3Fa%3D1%26b%3D2")]
    public void The_sign_in_url_escapes_the_return_url(string? returnUrl, string expected) =>
        Assert.Equal(expected, ExternalFailure.SignInUrl(ExternalFailure.Failed, returnUrl));
}
