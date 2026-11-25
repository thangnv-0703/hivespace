using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HiveSpace.BuildingBlocks.Messaging;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>The two /v1/shops endpoints over HTTP with real bearer tokens: AC-1, AC-3, AC-10, AC-11, AC-15 (technical-design §6).</summary>
public sealed class ShopApiTests : IAsyncLifetime
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
    public async Task Ac1_a_verified_buyer_registers_a_shop_and_gets_201_with_a_location()
    {
        var user = await SignedInAsync(verified: true);

        using var response = await user.PostShopAsync(new { name = "Tiệm Bánh An", sellerType = "individual" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/v1/shops/me", response.Headers.Location?.OriginalString);
        Assert.Equal(("Tiệm Bánh An", "individual", false, false), (
            body.GetProperty("name").GetString(), body.GetProperty("sellerType").GetString(),
            body.GetProperty("setup").GetProperty("pickupAddress").GetBoolean(), body.GetProperty("setup").GetProperty("payout").GetBoolean()));
        Assert.True(body.GetProperty("shopId").GetGuid() != Guid.Empty);
    }

    [Fact]
    public async Task Ac3_every_invalid_field_and_an_unknown_key_come_back_together_in_the_request_language()
    {
        var user = await SignedInAsync(verified: true);

        using var en = await user.PostShopAsync(new { name = "ab", sellerType = "x", ownerId = Guid.NewGuid() }, "en");
        using var vi = await user.PostShopAsync(new { name = "ab", sellerType = "x" }, "vi");

        Assert.Equal(HttpStatusCode.BadRequest, en.StatusCode);
        var error = await en.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("common.validation_failed", error.GetProperty("code").GetString());
        var details = error.GetProperty("details").EnumerateArray().ToList();
        Assert.Equal(
            [("ownerId", "common.validation_failed"), ("name", "identity.shop_name_too_short"), ("sellerType", "identity.seller_type_invalid")],
            details.Select(d => (d.GetProperty("field").GetString()!, d.GetProperty("code").GetString()!)));
        Assert.Equal("Use at least 3 characters.", details[1].GetProperty("message").GetString());
        var viDetails = (await vi.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("details").EnumerateArray().ToList();
        Assert.Equal("Dùng ít nhất 3 ký tự.", viDetails[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task An_unverified_buyer_gets_403_email_not_verified_in_both_languages()
    {
        var user = await SignedInAsync(verified: false);

        using var en = await user.PostShopAsync(new { name = "Tiệm Bánh An", sellerType = "individual" }, "en");
        using var vi = await user.PostShopAsync(new { name = "Tiệm Bánh An", sellerType = "individual" }, "vi");

        Assert.Equal(HttpStatusCode.Forbidden, en.StatusCode);
        var enError = await en.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(("identity.email_not_verified", "Verify your email address before registering a shop."), (enError.GetProperty("code").GetString(), enError.GetProperty("message").GetString()));
        Assert.Equal("Hãy xác minh địa chỉ email trước khi đăng ký gian hàng.", (await vi.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_409_codes_are_returned_with_their_messages()
    {
        var first = await SignedInAsync(verified: true);
        var second = await SignedInAsync(verified: true);
        var name = $"Cửa Hàng {Guid.NewGuid():N}";
        (await first.PostShopAsync(new { name, sellerType = "business" })).Dispose();

        using var again = await first.PostShopAsync(new { name = "Another", sellerType = "business" }, "en");
        using var taken = await second.PostShopAsync(new { name = name.ToUpperInvariant(), sellerType = "individual" }, "en");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(("identity.shop_already_owned", "You already have a shop."), Pair(await again.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(("identity.shop_name_taken", "This shop name is already taken. Choose another name."), Pair(await taken.Content.ReadFromJsonAsync<JsonElement>(Ct)));
    }

    [Fact]
    public async Task Ac10_a_buyer_only_token_is_403_forbidden_on_get_my_shop()
    {
        var user = await SignedInAsync(verified: true);

        using var response = await user.GetShopAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("common.forbidden", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Ac10_ac11_a_token_issued_after_registering_carries_Seller_and_reads_the_own_shop_with_the_checklist_false()
    {
        var user = await SignedInAsync(verified: true);
        (await user.PostShopAsync(new { name = "Tiệm Bánh An", sellerType = "business" })).Dispose();
        var seller = await user.WithFreshTokenAsync(this);

        using var response = await seller.GetShopAsync();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("Tiệm Bánh An", "business", false, false), (
            body.GetProperty("name").GetString(), body.GetProperty("sellerType").GetString(),
            body.GetProperty("setup").GetProperty("pickupAddress").GetBoolean(), body.GetProperty("setup").GetProperty("payout").GetBoolean()));
        // The token from before registering still has no Seller role: roles show after the next token exchange (D-2).
        using var stale = await user.GetShopAsync();
        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
    }

    [Fact]
    public async Task Ac10_a_spoofed_user_id_header_changes_neither_the_owner_created_nor_the_shop_read()
    {
        var alice = await SignedInAsync(verified: true);
        var bob = await SignedInAsync(verified: true);
        (await bob.PostShopAsync(new { name = $"Bob {Guid.NewGuid():N}", sellerType = "individual" })).Dispose();

        using var created = await alice.PostShopAsync(new { name = $"Alice {Guid.NewGuid():N}", sellerType = "individual" }, spoofedUserId: bob.UserId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var aliceSeller = await alice.WithFreshTokenAsync(this);
        using var read = await aliceSeller.GetShopAsync(spoofedUserId: bob.UserId);
        var body = await read.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.StartsWith("Alice", body.GetProperty("name").GetString(), StringComparison.Ordinal);
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(alice.UserId, (await db.Shops.SingleAsync(s => s.Id == body.GetProperty("shopId").GetGuid(), Ct)).OwnerUserId);
    }

    private static readonly object ProfileBody = new { name = "Tiệm Bánh An", description = (string?)null };
    private static readonly object AddressBody = new { contactName = "An", phone = "0912345678", province = "Hà Nội", district = "Ba Đình", ward = "Phúc Xá", street = "12 Lê Lợi" };
    private static readonly object PayoutBody = new { bankName = "Vietcombank", accountNumber = "0071001234567", accountHolder = "NGUYEN VAN AN" };

    [Theory]
    [InlineData("/v1/shops/me/profile")]
    [InlineData("/v1/shops/me/pickup-address")]
    [InlineData("/v1/shops/me/payout")]
    public async Task Ac8_a_buyer_only_token_is_403_forbidden_on_each_put_and_nothing_is_written(string path)
    {
        var user = await SignedInAsync(verified: true);
        var body = path.EndsWith("profile", StringComparison.Ordinal) ? ProfileBody : path.EndsWith("address", StringComparison.Ordinal) ? AddressBody : PayoutBody;

        using var response = await user.PutAsync(path, body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("common.forbidden", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("/v1/shops/me/profile")]
    [InlineData("/v1/shops/me/pickup-address")]
    [InlineData("/v1/shops/me/payout")]
    public async Task Ac8_a_spoofed_user_id_header_and_a_body_shop_id_never_change_the_shop_written(string path)
    {
        var alice = await SignedInAsync(verified: true);
        var bob = await SignedInAsync(verified: true);
        (await bob.PostShopAsync(new { name = $"Bob {Guid.NewGuid():N}", sellerType = "individual" })).Dispose();
        (await alice.PostShopAsync(new { name = $"Alice {Guid.NewGuid():N}", sellerType = "individual" })).Dispose();
        var aliceSeller = await alice.WithFreshTokenAsync(this);
        var body = path.EndsWith("profile", StringComparison.Ordinal)
            ? new Dictionary<string, object?> { ["name"] = $"Renamed {Guid.NewGuid():N}", ["description"] = "mine" }
            : path.EndsWith("address", StringComparison.Ordinal)
                ? new Dictionary<string, object?> { ["contactName"] = "An", ["phone"] = "0912345678", ["province"] = "p", ["district"] = "d", ["ward"] = "w", ["street"] = "s" }
                : new Dictionary<string, object?> { ["bankName"] = "Vietcombank", ["accountNumber"] = "0071001234567", ["accountHolder"] = "AN" };

        using var withShopId = await aliceSeller.PutAsync(path, new Dictionary<string, object?>(body) { ["shopId"] = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, withShopId.StatusCode);
        using var spoofed = await aliceSeller.PutAsync(path, body, spoofedUserId: bob.UserId);

        Assert.Equal(HttpStatusCode.OK, spoofed.StatusCode);
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var aliceShop = await db.Shops.SingleAsync(s => s.OwnerUserId == alice.UserId, Ct);
        var bobShop = await db.Shops.SingleAsync(s => s.OwnerUserId == bob.UserId, Ct);
        // The section this endpoint writes changed on the caller's shop, and only that one.
        var written = path.EndsWith("profile", StringComparison.Ordinal) ? aliceShop.Description == "mine"
            : path.EndsWith("address", StringComparison.Ordinal) ? aliceShop.PickupAddress is not null && aliceShop.Payout is null && aliceShop.Description is null
            : aliceShop.Payout is not null && aliceShop.PickupAddress is null && aliceShop.Description is null;
        Assert.True(written);
        Assert.Equal((null, null, null), (bobShop.Description, bobShop.PickupAddress, bobShop.Payout));
    }

    [Fact]
    public async Task Ac11_the_payout_response_and_the_logs_never_carry_the_account_digits()
    {
        var user = await SignedInAsync(verified: true);
        (await user.PostShopAsync(new { name = $"Shop {Guid.NewGuid():N}", sellerType = "individual" })).Dispose();
        var seller = await user.WithFreshTokenAsync(this);

        using var response = await seller.PutAsync("/v1/shops/me/payout", PayoutBody);
        var text = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("0071001234567", text, StringComparison.Ordinal);
        Assert.DoesNotContain("accountNumber\"", text, StringComparison.Ordinal);
        Assert.Contains("\"accountNumberLast4\":\"4567\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0071001234567", _factory!.Logs.Text, StringComparison.Ordinal);
        Assert.Contains("section payout", _factory.Logs.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("tampered")]
    [InlineData("wrong audience")]
    public async Task Ac10_no_tampered_and_wrong_audience_tokens_are_401_on_both_endpoints(string kind)
    {
        var user = await SignedInAsync(verified: true);
        var token = kind switch
        {
            "none" => null,
            "tampered" => Tamper(user.AccessToken),
            _ => Resign(user.AccessToken, "other-api"),
        };

        using var post = await user.RawAsync(HttpMethod.Post, "/v1/shops", token, JsonContent.Create(new { name = "Tiệm Bánh An", sellerType = "individual" }));
        using var get = await user.RawAsync(HttpMethod.Get, "/v1/shops/me", token, null);

        foreach (var response in new[] { post, get })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("common.unauthenticated", (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString());
        }
    }

    // ---- helpers ----

    private static (string?, string?) Pair(JsonElement error) => (error.GetProperty("code").GetString(), error.GetProperty("message").GetString());

    private static string Tamper(string jwt)
    {
        var parts = jwt.Split('.');
        var lastChar = parts[2][^1] == 'A' ? 'B' : 'A';
        return $"{parts[0]}.{parts[1]}.{parts[2][..^1]}{lastChar}";
    }

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

    private async Task<Session> SignedInAsync(bool verified)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var email = $"{Guid.NewGuid():N}@example.com";
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("userId").GetGuid();
        if (verified)
        {
            using var verify = await client.PostAsJsonAsync("/v1/users/verify-email", new { token = await TokenFromOutboxAsync(userId) }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
        }

        using var signIn = await client.PostAsJsonAsync("/v1/users/sign-in", new { email, password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return await ExchangeAsync(client, userId, email);
    }

    private async Task<string> TokenFromOutboxAsync(Guid userId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        foreach (var row in await db.Set<OutboxMessage>().Where(m => m.EventType == "UserRegistered").ToListAsync(Ct))
        {
            var data = EventEnvelope.FromJson(row.Envelope).Data;
            if (data.GetProperty("userId").GetGuid() == userId)
            {
                return data.GetProperty("verificationToken").GetString()!;
            }
        }

        throw new InvalidOperationException("No UserRegistered event for the user.");
    }

    /// <summary>Authorization-code exchange with the signed-in cookie client; a new exchange re-reads the roles from the database.</summary>
    private async Task<Session> ExchangeAsync(HttpClient cookieClient, Guid userId, string email)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorize = "/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
            + $"&code_challenge={challenge}&code_challenge_method=S256";
        using var redirect = await cookieClient.GetAsync(authorize, Ct);
        var code = System.Web.HttpUtility.ParseQueryString(redirect.Headers.Location!.Query)["code"]!;
        using var tokenResponse = await cookieClient.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = "hivespace-storefront",
            ["client_secret"] = IdentityApiFactory.ClientSecret,
        }), Ct);
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("access_token").GetString()!;
        var bearer = _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        return new Session(bearer, cookieClient, userId, email, accessToken);
    }

    private sealed record Session(HttpClient Client, HttpClient CookieClient, Guid UserId, string Email, string AccessToken)
    {
        public Task<Session> WithFreshTokenAsync(ShopApiTests test) => test.ExchangeAsync(CookieClient, UserId, Email);

        public Task<HttpResponseMessage> PostShopAsync(object body, string? language = null, Guid? spoofedUserId = null) =>
            Send(HttpMethod.Post, "/v1/shops", JsonContent.Create(body), language, spoofedUserId);

        public Task<HttpResponseMessage> PutAsync(string path, object body, Guid? spoofedUserId = null) =>
            Send(HttpMethod.Put, path, JsonContent.Create(body), null, spoofedUserId);

        public Task<HttpResponseMessage> GetShopAsync(Guid? spoofedUserId = null) =>
            Send(HttpMethod.Get, "/v1/shops/me", null, null, spoofedUserId);

        public Task<HttpResponseMessage> RawAsync(HttpMethod method, string path, string? token, HttpContent? content)
        {
            var request = new HttpRequestMessage(method, path) { Content = content };
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // The hs.idp cookie rides along and must not count.
            return CookieClient.SendAsync(request, TestContext.Current.CancellationToken);
        }

        private Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? content, string? language, Guid? spoofedUserId)
        {
            var request = new HttpRequestMessage(method, path) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            if (language is not null)
            {
                request.Headers.AcceptLanguage.ParseAdd(language);
            }

            if (spoofedUserId is { } id)
            {
                request.Headers.Add("X-User-Id", id.ToString());
            }

            return Client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }
}
