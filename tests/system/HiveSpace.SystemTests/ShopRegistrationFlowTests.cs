using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Npgsql;
using Xunit;

namespace HiveSpace.SystemTests;

/// <summary>
/// FEAT-009 T6, AC-11: a buyer signed in on the storefront reaches the seller host without a password (SSO through hs.idp, ADR-0027),
/// registers a shop through the gateway, refreshes the session and then reads the shop with the new Seller role.
/// </summary>
public sealed class ShopRegistrationFlowTests(StackFixture stack)
{
    private const string StorefrontHost = "localhost:5080";
    private const string SellerHost = "seller.localhost:5080";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_buyer_registers_a_shop_through_the_gateway_refreshes_the_session_and_reads_the_shop_on_the_seller_host()
    {
        var email = $"system-shop-{Guid.NewGuid():N}@example.com";
        // Redirects are followed by hand: every host reaches the same gateway endpoint, and the Location headers name the fixed local ports.
        using var gateway = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = stack.App.GetEndpoint("gateway") };
        var browser = new Browser(gateway);

        // A buyer with a verified email, signed in on the storefront host (hs.idp).
        using (var registered = await browser.SendAsync(StorefrontHost, HttpMethod.Post, "/v1/users", new { email, displayName = "Shop Test", password = "correct-horse-1" }))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        await VerifyEmailAsync(email);
        using (var signedIn = await browser.SendAsync(StorefrontHost, HttpMethod.Post, "/v1/users/sign-in", new { email, password = "correct-horse-1" }))
        {
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        }

        // The seller host signs in through Identity's session, with no password prompt.
        await browser.FollowAsync(SellerHost, "/bff/login?returnUrl=/");
        using (var user = await browser.SendAsync(SellerHost, HttpMethod.Get, "/bff/user"))
        {
            var body = await user.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(["Buyer"], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        }

        // Before the refresh the seller-only read is forbidden; registering needs no role.
        using (var forbidden = await browser.SendAsync(SellerHost, HttpMethod.Get, "/v1/shops/me"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        using (var created = await browser.SendAsync(SellerHost, HttpMethod.Post, "/v1/shops", new { name = $"Tiệm {Guid.NewGuid():N}", sellerType = "individual" }))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Equal("/v1/shops/me", created.Headers.Location?.OriginalString);
        }

        using (var refreshed = await browser.SendAsync(SellerHost, HttpMethod.Post, "/bff/session/refresh"))
        {
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(["Buyer", "Seller"], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Order());
        }

        using var shop = await browser.SendAsync(SellerHost, HttpMethod.Get, "/v1/shops/me");
        Assert.Equal(HttpStatusCode.OK, shop.StatusCode);
        var shopBody = await shop.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.False(shopBody.GetProperty("setup").GetProperty("pickupAddress").GetBoolean());
    }

    /// <summary>
    /// FEAT-010 T7, AC-8: a seller saves the three settings sections through the gateway on the seller host and reads them back;
    /// a PUT without a session is 401 and a PUT without the CSRF header is 403.
    /// </summary>
    [Fact]
    public async Task A_seller_saves_profile_pickup_address_and_payout_through_the_gateway_and_reads_them_back()
    {
        var email = $"system-settings-{Guid.NewGuid():N}@example.com";
        using var gateway = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { BaseAddress = stack.App.GetEndpoint("gateway") };
        var browser = new Browser(gateway);
        (await browser.SendAsync(StorefrontHost, HttpMethod.Post, "/v1/users", new { email, displayName = "Shop Test", password = "correct-horse-1" })).Dispose();
        await VerifyEmailAsync(email);
        (await browser.SendAsync(StorefrontHost, HttpMethod.Post, "/v1/users/sign-in", new { email, password = "correct-horse-1" })).Dispose();
        await browser.FollowAsync(SellerHost, "/bff/login?returnUrl=/");
        var name = $"Tiệm {Guid.NewGuid():N}";
        (await browser.SendAsync(SellerHost, HttpMethod.Post, "/v1/shops", new { name, sellerType = "individual" })).Dispose();
        (await browser.SendAsync(SellerHost, HttpMethod.Post, "/bff/session/refresh")).Dispose();

        var profile = new { name = name + " 2", description = "Bánh mì nóng" };
        var address = new { contactName = "An", phone = "0912 345 678", province = "Hà Nội", district = "Ba Đình", ward = "Phúc Xá", street = "12 Lê Lợi" };
        var payout = new { bankName = "Vietcombank", accountNumber = "0071 0012 34567", accountHolder = "NGUYEN VAN AN" };
        foreach (var (path, body) in new (string, object)[] { ("/v1/shops/me/profile", profile), ("/v1/shops/me/pickup-address", address), ("/v1/shops/me/payout", payout) })
        {
            using var saved = await browser.SendAsync(SellerHost, HttpMethod.Put, path, body);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.DoesNotContain("0071001234567", await saved.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }

        using var read = await browser.SendAsync(SellerHost, HttpMethod.Get, "/v1/shops/me");
        var shop = await read.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal((name + " 2", "Bánh mì nóng", "+84912345678", "4567", true, true), (
            shop.GetProperty("name").GetString(),
            shop.GetProperty("description").GetString(),
            shop.GetProperty("pickupAddress").GetProperty("phone").GetString(),
            shop.GetProperty("payout").GetProperty("accountNumberLast4").GetString(),
            shop.GetProperty("setup").GetProperty("pickupAddress").GetBoolean(),
            shop.GetProperty("setup").GetProperty("payout").GetBoolean()));
        Assert.DoesNotContain("0071001234567", shop.GetRawText(), StringComparison.Ordinal);

        using var noCsrf = await browser.SendAsync(SellerHost, HttpMethod.Put, "/v1/shops/me/profile", profile, csrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, noCsrf.StatusCode);
        var anonymous = new Browser(gateway);
        using var noSession = await anonymous.SendAsync(SellerHost, HttpMethod.Put, "/v1/shops/me/profile", profile);
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);
    }

    private async Task VerifyEmailAsync(string email)
    {
        await using var connection = new NpgsqlConnection(await stack.App.GetConnectionStringAsync("identitydb", Ct));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("UPDATE users SET email_verified_at = now() WHERE email = @email", connection);
        command.Parameters.AddWithValue("email", email);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(Ct));
    }

    /// <summary>A minimal browser: one cookie jar per host, redirects followed by hand because every host reaches the same gateway endpoint.</summary>
    private sealed class Browser(HttpClient gateway)
    {
        private readonly Dictionary<string, Dictionary<string, string>> _jars = new(StringComparer.OrdinalIgnoreCase);

        public async Task<HttpResponseMessage> SendAsync(string host, HttpMethod method, string path, object? body = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, new Uri(gateway.BaseAddress!, path)) { Content = body is null ? null : JsonContent.Create(body) };
            request.Headers.Host = host;
            if (csrf)
            {
                request.Headers.Add("X-HS-CSRF", "1");
            }

            request.Headers.Add("Accept-Language", "en");
            if (_jars.TryGetValue(host, out var jar) && jar.Count > 0)
            {
                request.Headers.Add("Cookie", string.Join("; ", jar.Select(c => $"{c.Key}={c.Value}")));
            }

            var response = await gateway.SendAsync(request, Ct);
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                jar ??= _jars[host] = new Dictionary<string, string>();
                foreach (var cookie in cookies)
                {
                    var pair = cookie.Split(';')[0];
                    var at = pair.IndexOf('=', StringComparison.Ordinal);
                    var name = pair[..at];
                    var value = pair[(at + 1)..];
                    if (value.Length == 0 || cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
                    {
                        jar.Remove(name);
                    }
                    else
                    {
                        jar[name] = value;
                    }
                }
            }

            return response;
        }

        /// <summary>GET <paramref name="path"/> on <paramref name="host"/> and follow every redirect, on whatever host it names, to the end.</summary>
        public async Task FollowAsync(string host, string path)
        {
            for (var hop = 0; hop < 10; hop++)
            {
                HttpResponseMessage response;
                try
                {
                    response = await SendAsync(host, HttpMethod.Get, path);
                }
                catch (HttpRequestException e)
                {
                    throw new InvalidOperationException($"GET {host}{path}", e);
                }

                using var _ = response;
                if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther))
                {
                    Assert.True(response.IsSuccessStatusCode, $"GET http://{host}{path} ended with {response.StatusCode}");
                    return;
                }

                var target = response.Headers.Location!;
                if (target.IsAbsoluteUri)
                {
                    host = target.Authority;
                    path = target.PathAndQuery;
                }
                else
                {
                    path = target.OriginalString;
                }
            }

            throw new InvalidOperationException("Too many redirects.");
        }
    }
}
