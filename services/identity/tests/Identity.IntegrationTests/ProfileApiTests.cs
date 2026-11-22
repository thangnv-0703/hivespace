using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>The three /v1/me endpoints over HTTP with real bearer tokens: AC-1..AC-5, AC-9, AC-10 (technical-design §6).</summary>
public sealed class ProfileApiTests : IAsyncLifetime
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
    public async Task Ac1_get_returns_the_profile_of_the_token_subject()
    {
        var user = await SignedInAsync("An Nguyen");

        using var response = await user.GetAsync("/v1/me/profile");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.UserId, body.GetProperty("userId").GetGuid());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal("An Nguyen", body.GetProperty("displayName").GetString());
        Assert.Equal("vi", body.GetProperty("language").GetString());
        foreach (var nullable in new[] { "phone", "gender", "dateOfBirth", "avatarUrl" })
        {
            Assert.Equal(JsonValueKind.Null, body.GetProperty(nullable).ValueKind);
        }
    }

    [Fact]
    public async Task Ac2_put_saves_and_get_returns_the_values_and_null_clears()
    {
        var user = await SignedInAsync();

        using var saved = await user.PutAsync("/v1/me/profile", new { displayName = "Lan Tran", phone = "0912 345 678", gender = "female", dateOfBirth = "1995-04-30" });
        var read = await (await user.GetAsync("/v1/me/profile")).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(("Lan Tran", "+84912345678", "female", "1995-04-30"), (
            read.GetProperty("displayName").GetString(), read.GetProperty("phone").GetString(),
            read.GetProperty("gender").GetString(), read.GetProperty("dateOfBirth").GetString()));

        using var cleared = await user.PutAsync("/v1/me/profile", new { displayName = "Lan Tran", phone = (string?)null, gender = (string?)null, dateOfBirth = (string?)null });
        var after = await cleared.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("phone").ValueKind);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("gender").ValueKind);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("dateOfBirth").ValueKind);
    }

    [Fact]
    public async Task Ac3_every_invalid_field_is_reported_in_one_400_and_ac10_messages_follow_the_language()
    {
        var user = await SignedInAsync();
        var invalid = new { displayName = "", phone = "12345", gender = "x", dateOfBirth = "2999-01-01" };

        using var en = await user.PutAsync("/v1/me/profile", invalid, "en");
        using var vi = await user.PutAsync("/v1/me/profile", invalid, "vi");

        Assert.Equal(HttpStatusCode.BadRequest, en.StatusCode);
        var error = await en.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("common.validation_failed", error.GetProperty("code").GetString());
        var details = error.GetProperty("details").EnumerateArray().ToList();
        Assert.Equal(
            [("displayName", "identity.display_name_required"), ("phone", "identity.phone_invalid"), ("gender", "identity.gender_invalid"), ("dateOfBirth", "identity.date_of_birth_in_future")],
            details.Select(d => (d.GetProperty("field").GetString()!, d.GetProperty("code").GetString()!)));
        Assert.Equal("Enter a Vietnamese phone number, for example 0912 345 678 or +84 912 345 678.", details[1].GetProperty("message").GetString());
        var viDetails = (await vi.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("details").EnumerateArray().ToList();
        Assert.Equal("Nhập số điện thoại Việt Nam, ví dụ 0912 345 678 hoặc +84 912 345 678.", viDetails[1].GetProperty("message").GetString());

        var after = await (await user.GetAsync("/v1/me/profile")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("An", after.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Ac4_an_email_key_is_read_only_and_the_email_is_unchanged()
    {
        var user = await SignedInAsync();

        using var response = await user.PutAsync("/v1/me/profile", new { displayName = "An", phone = (string?)null, gender = (string?)null, dateOfBirth = (string?)null, email = "other@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("details").EnumerateArray().Single();
        Assert.Equal(("email", "identity.email_read_only"), (details.GetProperty("field").GetString(), details.GetProperty("code").GetString()));
        var read = await (await user.GetAsync("/v1/me/profile")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(user.Email, read.GetProperty("email").GetString());
    }

    [Fact]
    public async Task A_missing_or_mistyped_key_is_a_field_error()
    {
        var user = await SignedInAsync();

        using var missing = await user.PutAsync("/v1/me/profile", new { displayName = "An" });
        using var typed = await user.PutAsync("/v1/me/profile", new { displayName = "An", phone = 912345678, gender = (string?)null, dateOfBirth = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, typed.StatusCode);
        var fields = (await typed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("details").EnumerateArray().Select(d => d.GetProperty("field").GetString());
        Assert.Equal(["phone"], fields);
    }

    [Fact]
    public async Task Ac5_language_is_saved_a_repeat_is_ok_and_fr_is_rejected()
    {
        var user = await SignedInAsync();

        using var en = await user.PutAsync("/v1/me/profile/language", new { language = "en" });
        using var again = await user.PutAsync("/v1/me/profile/language", new { language = "en" });
        using var fr = await user.PutAsync("/v1/me/profile/language", new { language = "fr" });

        Assert.Equal("en", (await en.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("language").GetString());
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fr.StatusCode);
        var detail = (await fr.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("details").EnumerateArray().Single();
        Assert.Equal("identity.language_unsupported", detail.GetProperty("code").GetString());
        var read = await (await user.GetAsync("/v1/me/profile")).Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("en", read.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Ac9_a_spoofed_user_id_header_never_changes_whose_profile_is_used()
    {
        var alice = await SignedInAsync("Alice");
        var bob = await SignedInAsync("Bob");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        request.Headers.Add("X-User-Id", bob.UserId.ToString());
        using var response = await alice.Client.SendAsync(request, Ct);

        Assert.Equal(alice.UserId, (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Ac9_no_token_a_tampered_token_a_wrong_audience_token_and_the_session_cookie_alone_are_all_401()
    {
        var user = await SignedInAsync();
        var parts = user.AccessToken.Split('.');
        var lastChar = parts[2][^1] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{parts[1]}.{parts[2][..^1]}{lastChar}";
        var wrongAudience = Resign(user.AccessToken, aud: "other-api");

        foreach (var (name, token) in new[] { ("none", (string?)null), ("tampered", tampered), ("wrong audience", wrongAudience), ("garbage", "not-a-jwt") })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me/profile");
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await user.CookieClient.SendAsync(request, Ct); // the hs.idp cookie rides along and must not count
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name}: {response.StatusCode}");
            Assert.Equal("common.unauthenticated", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }

        // Control: the unmodified token works through the same client, so the failures above are about the token.
        using var good = new HttpRequestMessage(HttpMethod.Get, "/v1/me/profile");
        good.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var ok = await user.CookieClient.SendAsync(good, Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    // ---- helpers ----

    /// <summary>A freshly re-signed copy of the access token with another audience, using the server's own key.</summary>
    private string Resign(string jwt, string aud)
    {
        var parts = jwt.Split('.');
        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Convert.FromBase64String(Pad(parts[1])))!;
        var changed = payload.ToDictionary(p => p.Key, p => (object)p.Value);
        changed["aud"] = aud;
        var signingInput = parts[0] + "." + Base64Url(JsonSerializer.SerializeToUtf8Bytes(changed));
        var signature = _factory!.SigningKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return signingInput + "." + Base64Url(signature);
    }

    private static string Pad(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<Session> SignedInAsync(string displayName = "An")
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid();
        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var verifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorize = "/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
            + $"&code_challenge={challenge}&code_challenge_method=S256";
        using var redirect = await client.GetAsync(authorize, Ct);
        var code = System.Web.HttpUtility.ParseQueryString(redirect.Headers.Location!.Query)["code"]!;
        using var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = "hivespace-storefront",
            ["client_secret"] = IdentityApiFactory.ClientSecret,
        }), Ct);
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;

        // A second client without cookies: only the bearer token identifies the caller.
        var bearer = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        return new Session(bearer, client, userId, email, accessToken);
    }

    private sealed record Session(HttpClient Client, HttpClient CookieClient, Guid UserId, string Email, string AccessToken)
    {
        public Task<HttpResponseMessage> GetAsync(string path) => Send(HttpMethod.Get, path, null, null);

        public Task<HttpResponseMessage> PutAsync(string path, object body, string? language = null) => Send(HttpMethod.Put, path, body, language);

        private Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body, string? language)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            if (language is not null)
            {
                request.Headers.AcceptLanguage.ParseAdd(language);
            }

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            return Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
