using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.Testing;
using Xunit;

namespace HiveSpace.SystemTests;

/// <summary>
/// FEAT-006 T10, AC-14: request a sign-in code through the gateway, follow SignInCodeRequested through Identity's outbox, the Service
/// Bus emulator and Notification to Mailpit, then sign in with the code from the email.
/// </summary>
public sealed partial class CodeSignInFlowTests(StackFixture stack)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"\b(?<code>[0-9]{6})\b")]
    private static partial Regex CodePattern();

    [Fact]
    public async Task A_requested_code_arrives_in_mailpit_and_signs_in()
    {
        var email = $"system-code-{Guid.NewGuid():N}@example.com";
        using var gateway = stack.App.CreateHttpClient("gateway");
        using (var registered = await gateway.SendAsync(Post("/v1/users", new { email, displayName = "Code Test", password = "correct-horse-1" }), Ct))
        {
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }

        using var requested = await gateway.SendAsync(Post("/v1/users/sign-in/code-challenges", new { email }), Ct);
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var challengeId = (await requested.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("challengeId").GetString();

        using var mailpit = stack.App.CreateHttpClient("mailpit", "http");
        var message = await WaitForCodeEmailAsync(mailpit, email);
        Assert.Equal("Your HiveSpace sign-in code", message.GetProperty("Subject").GetString());
        var code = CodePattern().Match(message.GetProperty("Text").GetString()!).Groups["code"].Value;
        Assert.Matches("^[0-9]{6}$", code);

        using var signedIn = await gateway.SendAsync(Post("/v1/users/sign-in/code", new { challengeId, code }), Ct);

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Contains(signedIn.Headers.GetValues("Set-Cookie"), c => c.StartsWith("hs.idp=", StringComparison.Ordinal));

        // AC-10 / NFR-SEC-016: the code and the email appear in no service's console log.
        foreach (var service in new[] { "gateway", "identity", "notification" })
        {
            var lines = stack.LogsOf(service);
            Assert.DoesNotContain(lines, line => line.Contains(email, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(lines, line => line.Contains("sign-in code is", StringComparison.Ordinal) || line.Contains($" {code} ", StringComparison.Ordinal));
        }
    }

    private static HttpRequestMessage Post(string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-HS-CSRF", "1");
        request.Headers.Add("Accept-Language", "en");
        return request;
    }

    /// <summary>The registration email may arrive first; wait for the code email to the same address.</summary>
    private static async Task<JsonElement> WaitForCodeEmailAsync(HttpClient mailpit, string email)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            using var search = JsonDocument.Parse(await mailpit.GetStringAsync(
                $"/api/v1/search?query={Uri.EscapeDataString($"to:{email} subject:\"sign-in code\"")}", Ct));
            var first = search.RootElement.GetProperty("messages").EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                var id = first.GetProperty("ID").GetString();
                return JsonDocument.Parse(await mailpit.GetStringAsync($"/api/v1/message/{id}", Ct)).RootElement.Clone();
            }

            await Task.Delay(500, Ct);
        }

        throw new TimeoutException($"No sign-in code email for {email} reached Mailpit within 90 s.");
    }
}
