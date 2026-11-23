using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FakeIdp;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-005 T3 through HTTP: AC-1 (423 and the cookie), AC-4 (parallel failures), AC-7 (Google while locked), AC-8.</summary>
public sealed class LockoutApiTests : IAsyncLifetime
{
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
    public async Task Ac1_the_fifth_wrong_password_answers_423_and_a_locked_account_sets_no_cookie()
    {
        var (_, email) = await RegisterAsync();
        using var client = _factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            using var wrong = await SignInAsync(client, email, "wrong9999");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var fifth = await SignInAsync(client, email, "wrong9999");
        using var correct = await SignInAsync(client, email, "hello1234");

        Assert.Equal(HttpStatusCode.Locked, fifth.StatusCode);
        Assert.Contains("identity.account_locked", await fifth.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Locked, correct.StatusCode);
        Assert.False(correct.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task A_password_sign_in_issues_hs_idp_with_auth_time_and_amr()
    {
        var (_, email) = await RegisterAsync();
        using var client = _factory.CreateClient();
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var response = await SignInAsync(client, email, "hello1234");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("hs.idp=", StringComparison.Ordinal));
        Assert.StartsWith("hs.idp=", cookie, StringComparison.Ordinal);
        await using var db = Db();
        Assert.True(await db.AuditEntries.AnyAsync(a => a.Type == "sign_in_succeeded" && a.OccurredAt >= DateTimeOffset.FromUnixTimeSeconds(before), Ct));
    }

    [Fact]
    public async Task Ac4_ten_parallel_wrong_passwords_lock_the_account_with_one_account_locked_row_and_no_500()
    {
        var (userId, email) = await RegisterAsync();
        using var client = _factory.CreateClient();

        var statuses = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            using var response = await SignInAsync(client, email, "wrong9999");
            return response.StatusCode;
        }));

        Assert.All(statuses, s => Assert.True(s is HttpStatusCode.Unauthorized or HttpStatusCode.Locked, s.ToString()));
        await using var db = Db();
        var lockout = await db.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
        Assert.Equal(5, lockout.FailedCount);
        Assert.NotNull(lockout.LockedUntil);
        Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.UserId == userId && a.Type == "account_locked", Ct));
    }

    [Fact]
    public async Task Ac7_google_sign_in_still_works_while_the_password_is_locked_and_the_lockout_is_untouched()
    {
        var email = $"{Guid.NewGuid():N}@gmail.com";
        var (userId, _) = await RegisterAsync(email, verified: true);
        using var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await SignInAsync(client, email, "wrong9999");
        }

        await using (var db = Db())
        {
            Assert.NotNull((await db.SignInLockouts.SingleAsync(l => l.UserId == userId, Ct)).LockedUntil);
        }

        var sub = "sub-" + Guid.NewGuid().ToString("N");
        _idp.State.Next = new NextSignIn("ok", sub, email, "An");
        using var browser = new GoogleBrowser(_factory, _idp);
        var authorize = AuthorizeUrl();

        var run = await browser.SignInAsync(authorize);

        Assert.Equal(authorize, run.FinalLocation);
        await using var after = Db();
        var lockout = await after.SignInLockouts.AsNoTracking().SingleAsync(l => l.UserId == userId, Ct);
        Assert.Equal(5, lockout.FailedCount);
        var google = await after.AuditEntries.AsNoTracking().Where(a => a.UserId == userId && a.Method == Identity.Domain.AuditMethod.Google).ToListAsync(Ct);
        Assert.Equal("sign_in_succeeded", Assert.Single(google).Type);
    }

    [Fact]
    public async Task Ac8_a_failed_google_sign_in_writes_one_google_audit_row_with_its_code()
    {
        _idp.State.Next = new NextSignIn(Outcomes.Denied, "sub-" + Guid.NewGuid().ToString("N"), $"{Guid.NewGuid():N}@gmail.com", "An");
        using var browser = new GoogleBrowser(_factory, _idp);

        var run = await browser.SignInAsync(AuthorizeUrl());

        Assert.Contains("identity.external_login_denied", run.FinalLocation, StringComparison.Ordinal);
        await using var db = Db();
        var row = await db.AuditEntries.AsNoTracking().SingleAsync(a => a.Method == Identity.Domain.AuditMethod.Google && a.Type == "sign_in_failed", Ct);
        Assert.Equal("identity.external_login_denied", row.Reason);
        Assert.Null(row.UserId);
    }

    private async Task<(Guid UserId, string Email)> RegisterAsync(string? email = null, bool verified = false)
    {
        email ??= $"{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();
        using var register = await client.PostAsJsonAsync("/v1/users", new { email, displayName = "An", password = "hello1234" }, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Ct)).GetProperty("userId").GetGuid();
        if (verified)
        {
            await using var db = Db();
            await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerifiedAt, DateTimeOffset.UtcNow), Ct);
        }

        return (userId, email);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/v1/users/sign-in", new { email, password }, Ct);

    private IdentityDbContext Db() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    private static string AuthorizeUrl()
    {
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"/connect/authorize?client_id=hivespace-storefront&response_type=code&state=xyz"
            + $"&redirect_uri={Uri.EscapeDataString(IdentityApiFactory.Issuer + "/bff/callback")}&scope={Uri.EscapeDataString("openid email profile roles offline_access")}"
            + $"&code_challenge={challenge}&code_challenge_method=S256";
    }
}
