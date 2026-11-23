using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FakeIdp.Tests;

/// <summary>
/// The fake IdP behaves as the Identity integration tests expect (FEAT-004 T4): one full code flow for <c>ok</c>, and each failure
/// outcome produces the response that really fails the matching check.
/// </summary>
public sealed class CodeFlowTests : IAsyncLifetime
{
    private const string ClientId = "hivespace-test";
    private const string Secret = "fake-secret"; // pragma: allowlist secret - test value
    private const string RedirectUri = "http://localhost:5080/connect/external/google/callback";

    private FakeIdpServer _idp = null!;
    private HttpClient _http = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _idp = await FakeIdpServer.StartAsync(ClientId, Secret, RedirectUri);
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(_idp.Authority) };
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _idp.DisposeAsync();
    }

    [Fact]
    public async Task Ok_runs_one_full_code_flow_with_a_valid_id_token()
    {
        var configuration = await DiscoverAsync();
        Assert.Equal(_idp.Authority, configuration.Issuer);
        Assert.Equal(_idp.Authority + "/authorize", configuration.AuthorizationEndpoint);
        Assert.Contains("S256", configuration.CodeChallengeMethodsSupported);

        var flow = await AuthorizeAsync();
        Assert.Equal("the-state", flow.Query["state"]);
        var tokens = await RedeemAsync(flow.Query["code"]!, flow.Verifier);

        Assert.Equal(HttpStatusCode.OK, tokens.Status);
        var result = await ValidateAsync(tokens.IdToken!, configuration);
        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal("fake-google-sub-1", jwt.Subject);
        Assert.Equal("an.nguyen@gmail.com", jwt.GetClaim("email").Value);
        Assert.Equal("true", jwt.GetClaim("email_verified").Value);
        Assert.Equal("An Nguyen", jwt.GetClaim("name").Value);
        Assert.Equal(flow.Nonce, jwt.GetClaim("nonce").Value);
    }

    [Fact]
    public async Task Next_sets_the_identity_of_the_following_sign_ins()
    {
        await NextAsync("ok", sub: "sub-42", email: "binh@gmail.com", name: "Binh");

        var jwt = new JsonWebToken((await RunAsync()).IdToken);

        Assert.Equal(("sub-42", "binh@gmail.com", "Binh"), (jwt.Subject, jwt.GetClaim("email").Value, jwt.GetClaim("name").Value));
    }

    [Fact]
    public async Task A_fake_next_cookie_overrides_the_shared_next_for_that_browser()
    {
        await NextAsync("ok", sub: "shared-sub");
        var cookie = JsonSerializer.Serialize(new { outcome = "ok", sub = "browser-sub", email = "chi@gmail.com", name = "Chi" });

        var own = await RunAsync(cookie: Uri.EscapeDataString(cookie));
        var shared = await RunAsync();

        Assert.Equal("browser-sub", new JsonWebToken(own.IdToken).Subject);
        Assert.Equal("chi@gmail.com", new JsonWebToken(own.IdToken).GetClaim("email").Value);
        Assert.Equal("shared-sub", new JsonWebToken(shared.IdToken).Subject);
    }

    [Theory]
    [InlineData(Outcomes.BadSignature, typeof(SecurityTokenInvalidSignatureException))]
    [InlineData(Outcomes.WrongAudience, typeof(SecurityTokenInvalidAudienceException))]
    [InlineData(Outcomes.WrongIssuer, typeof(SecurityTokenInvalidIssuerException))]
    [InlineData(Outcomes.Expired, typeof(SecurityTokenExpiredException))]
    public async Task Token_outcomes_fail_the_matching_validation(string outcome, Type expected)
    {
        await NextAsync(outcome);
        var configuration = await DiscoverAsync();

        var result = await ValidateAsync((await RunAsync()).IdToken!, configuration);

        Assert.False(result.IsValid);
        Assert.IsAssignableFrom(expected, result.Exception);
    }

    [Fact]
    public async Task Wrong_nonce_echoes_another_nonce()
    {
        await NextAsync(Outcomes.WrongNonce);
        var flow = await AuthorizeAsync();

        var jwt = new JsonWebToken((await RedeemAsync(flow.Query["code"]!, flow.Verifier)).IdToken);

        Assert.NotEqual(flow.Nonce, jwt.GetClaim("nonce").Value);
    }

    [Fact]
    public async Task Claim_outcomes_change_only_their_claims()
    {
        await NextAsync(Outcomes.Unverified);
        var unverified = new JsonWebToken((await RunAsync()).IdToken);
        await NextAsync(Outcomes.NoEmail);
        var noEmail = new JsonWebToken((await RunAsync()).IdToken);
        await NextAsync(Outcomes.NoSub);
        var noSub = new JsonWebToken((await RunAsync()).IdToken);

        Assert.Equal("false", unverified.GetClaim("email_verified").Value);
        Assert.False(noEmail.TryGetClaim("email", out _));
        Assert.False(noEmail.TryGetClaim("email_verified", out _));
        Assert.False(noSub.TryGetClaim("sub", out _));
    }

    [Theory]
    [InlineData(Outcomes.Denied, "access_denied")]
    [InlineData(Outcomes.ServerError, "server_error")]
    public async Task Authorize_outcomes_redirect_back_with_the_error_and_the_state(string outcome, string error)
    {
        await NextAsync(outcome);

        var flow = await AuthorizeAsync();

        Assert.Equal(error, flow.Query["error"]);
        Assert.Equal("the-state", flow.Query["state"]);
        Assert.False(flow.Query.ContainsKey("code"));
    }

    [Fact]
    public async Task Down_answers_503_on_discovery_jwks_and_token_until_the_next_outcome()
    {
        var flow = await AuthorizeAsync();
        await NextAsync(Outcomes.Down);

        using var discovery = await _http.GetAsync(new Uri("/.well-known/openid-configuration", UriKind.Relative), Ct);
        using var jwks = await _http.GetAsync(new Uri("/jwks", UriKind.Relative), Ct);
        var token = await RedeemAsync(flow.Query["code"]!, flow.Verifier);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, discovery.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, jwks.StatusCode);
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "temporarily_unavailable"), (token.Status, token.Error));

        await NextAsync(Outcomes.Ok);
        Assert.Equal(HttpStatusCode.OK, (await RunAsync()).Status);
    }

    [Fact]
    public async Task Token_checks_the_pkce_verifier_the_secret_the_redirect_uri_and_single_use()
    {
        var flow = await AuthorizeAsync();
        Assert.Equal("invalid_grant", (await RedeemAsync(flow.Query["code"]!, "a-different-verifier-0000000000000000000000")).Error);

        flow = await AuthorizeAsync();
        Assert.Equal("invalid_client", (await RedeemAsync(flow.Query["code"]!, flow.Verifier, secret: "wrong")).Error);

        flow = await AuthorizeAsync();
        Assert.Equal("invalid_grant", (await RedeemAsync(flow.Query["code"]!, flow.Verifier, redirectUri: "http://localhost:5080/other")).Error);

        flow = await AuthorizeAsync();
        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(flow.Query["code"]!, flow.Verifier)).Status);
        Assert.Equal("invalid_grant", (await RedeemAsync(flow.Query["code"]!, flow.Verifier)).Error);
    }

    [Fact]
    public async Task Token_accepts_client_secret_basic()
    {
        var flow = await AuthorizeAsync();

        var tokens = await RedeemAsync(flow.Query["code"]!, flow.Verifier, basic: true);

        Assert.Equal(HttpStatusCode.OK, tokens.Status);
    }

    [Theory]
    [InlineData("other-client", RedirectUri)]
    [InlineData(ClientId, "http://evil.example/callback")]
    public async Task Authorize_refuses_an_unknown_client_or_redirect_uri_without_redirecting(string clientId, string redirectUri)
    {
        using var response = await _http.GetAsync(new Uri("/authorize?" + AuthorizeQuery(clientId, redirectUri, "c", "n"), UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Last_records_the_parameters_but_never_the_client_secret()
    {
        var flow = await AuthorizeAsync();
        await RedeemAsync(flow.Query["code"]!, flow.Verifier);

        var seen = await _http.GetFromJsonAsync<JsonElement>(new Uri("/__fake/last", UriKind.Relative), Ct);

        Assert.Equal(RedirectUri, seen.GetProperty("authorize").GetProperty("redirect_uri").GetString());
        Assert.Equal(RedirectUri, seen.GetProperty("token").GetProperty("redirect_uri").GetString());
        Assert.DoesNotContain(Secret, seen.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stopped_server_refuses_connections_and_starts_again_with_the_same_keys()
    {
        var before = await DiscoverAsync();
        var authority = _idp.Authority;

        await _idp.StopAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => _http.GetAsync(new Uri("/jwks", UriKind.Relative), Ct));
        await FakeIdpServer.StartAsync(_idp);

        Assert.Equal(authority, _idp.Authority);
        Assert.True((await ValidateAsync((await RunAsync()).IdToken!, before)).IsValid);
    }

    private sealed record Flow(Dictionary<string, string?> Query, string Verifier, string Nonce);

    private sealed record Tokens(HttpStatusCode Status, string? IdToken, string? Error);

    private async Task<Tokens> RunAsync(string? cookie = null)
    {
        var flow = await AuthorizeAsync(cookie);
        return await RedeemAsync(flow.Query["code"]!, flow.Verifier);
    }

    private async Task<Flow> AuthorizeAsync(string? cookie = null)
    {
        var verifier = B64(RandomNumberGenerator.GetBytes(32));
        var challenge = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var nonce = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/authorize?" + AuthorizeQuery(ClientId, RedirectUri, challenge, nonce), UriKind.Relative));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{FakeIdpApp.NextCookie}={cookie}");
        }

        using var response = await _http.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(RedirectUri + "?", location.ToString(), StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(location.Query).ToDictionary(q => q.Key, q => (string?)q.Value.ToString());
        return new Flow(query, verifier, nonce);
    }

    private static string AuthorizeQuery(string clientId, string redirectUri, string challenge, string nonce) =>
        string.Join('&', new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = "the-state",
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

    private async Task<Tokens> RedeemAsync(string code, string verifier, string secret = Secret, string redirectUri = RedirectUri, bool basic = false)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/token", UriKind.Relative));
        if (basic)
        {
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{secret}")));
        }
        else
        {
            form["client_id"] = ClientId;
            form["client_secret"] = secret;
        }

        request.Content = new FormUrlEncodedContent(form);
        using var response = await _http.SendAsync(request, Ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return new Tokens(
            response.StatusCode,
            body.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null,
            body.TryGetProperty("error", out var error) ? error.GetString() : null);
    }

    private async Task NextAsync(string outcome, string? sub = null, string? email = null, string? name = null)
    {
        using var response = await _http.PostAsJsonAsync(new Uri("/__fake/next", UriKind.Relative), new { outcome, sub, email, name }, Ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<OpenIdConnectConfiguration> DiscoverAsync() =>
        await new ConfigurationManager<OpenIdConnectConfiguration>(
                _idp.Authority + "/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = false })
            .GetConfigurationAsync(Ct);

    private static Task<TokenValidationResult> ValidateAsync(string idToken, OpenIdConnectConfiguration configuration) =>
        new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudience = ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ClockSkew = TimeSpan.FromSeconds(60),
        });

    private static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
