using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting.Testing;
using Json.Schema;
using Npgsql;
using Xunit;

namespace HiveSpace.SystemTests;

/// <summary>
/// FEAT-001 T10, AC-6 and AC-7: register through the gateway, then follow the event Identity outbox -> Service Bus emulator
/// -> Notification -> Mailpit and use the link from the email.
/// </summary>
public sealed partial class RegistrationFlowTests(StackFixture stack)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"https?://\S+/verify-email\?token=(?<token>[^\s""<]+)")]
    private static partial Regex LinkPattern();

    [Fact]
    public async Task Registering_sends_exactly_one_email_with_a_working_link_from_a_schema_valid_event()
    {
        var email = $"system-{Guid.NewGuid():N}@example.com";
        using var gateway = stack.App.CreateHttpClient("gateway");
        using var register = new HttpRequestMessage(HttpMethod.Post, "/v1/users")
        {
            Content = JsonContent.Create(new { email, displayName = "System Test", password = "correct-horse-1" }),
        };
        register.Headers.Add("X-HS-CSRF", "1");
        register.Headers.Add("Accept-Language", "en");

        using var registered = await gateway.SendAsync(register, Ct);

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        // One email, found in Mailpit; then wait out a duplicate that could still be in flight.
        using var mailpit = stack.App.CreateHttpClient("mailpit", "http");
        var message = await WaitForEmailAsync(mailpit, email);
        await Task.Delay(TimeSpan.FromSeconds(5), Ct);
        Assert.Equal(1, await CountEmailsAsync(mailpit, email));
        Assert.Equal("Verify your HiveSpace email", message.GetProperty("Subject").GetString());

        // The link in the email works.
        var token = Uri.UnescapeDataString(LinkPattern().Match(message.GetProperty("Text").GetString()!).Groups["token"].Value);
        Assert.NotEmpty(token);

        // The browser first loads the page from the link: the token is in the URL the gateway serves.
        using var page = await gateway.GetAsync($"/verify-email?token={Uri.EscapeDataString(token)}", Ct);
        Assert.True(page.IsSuccessStatusCode, $"GET /verify-email returned {page.StatusCode}");
        using var verify = new HttpRequestMessage(HttpMethod.Post, "/v1/users/verify-email")
        {
            Content = JsonContent.Create(new { token }),
        };
        verify.Headers.Add("X-HS-CSRF", "1");
        using var verified = await gateway.SendAsync(verify, Ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);

        // The published event matches its contract and carries the trace that the Notification consumer continues.
        var envelope = await ReadOutboxEnvelopeAsync(email);
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(RepoPath("contracts", "events", "UserRegistered", "v1.schema.json"), Ct));
        var evaluation = schema.Evaluate(envelope.GetProperty("data"));
        Assert.True(evaluation.IsValid, "UserRegistered data does not match contracts/events/UserRegistered/v1.schema.json");
        Assert.Matches(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", envelope.GetProperty("traceparent").GetString());

        // AC-8 / R-6: the token appears in no service's console log.
        foreach (var service in new[] { "gateway", "identity", "notification" })
        {
            var lines = stack.LogsOf(service);
            Assert.NotEmpty(lines);
            Assert.DoesNotContain(lines, line => line.Contains(token, StringComparison.Ordinal));
        }
    }

    private static async Task<JsonElement> WaitForEmailAsync(HttpClient mailpit, string email)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            using var search = JsonDocument.Parse(await mailpit.GetStringAsync($"/api/v1/search?query={Uri.EscapeDataString("to:" + email)}", Ct));
            var first = search.RootElement.GetProperty("messages").EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                var id = first.GetProperty("ID").GetString();
                return JsonDocument.Parse(await mailpit.GetStringAsync($"/api/v1/message/{id}", Ct)).RootElement.Clone();
            }

            await Task.Delay(500, Ct);
        }

        throw new TimeoutException($"No email for {email} reached Mailpit within 90 s.");
    }

    private static async Task<int> CountEmailsAsync(HttpClient mailpit, string email)
    {
        using var search = JsonDocument.Parse(await mailpit.GetStringAsync($"/api/v1/search?query={Uri.EscapeDataString("to:" + email)}", Ct));
        return search.RootElement.GetProperty("messages_count").GetInt32();
    }

    private async Task<JsonElement> ReadOutboxEnvelopeAsync(string email)
    {
        var connectionString = await stack.App.GetConnectionStringAsync("identitydb", Ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT \"Envelope\"::text FROM outbox WHERE \"EventType\" = 'UserRegistered' AND \"Envelope\" -> 'data' ->> 'email' = @email", connection);
        command.Parameters.AddWithValue("email", email);
        var json = (string?)await command.ExecuteScalarAsync(Ct);
        Assert.NotNull(json);
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string RepoPath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HiveSpace.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine([dir?.FullName ?? throw new InvalidOperationException("Repository root not found."), .. parts]);
    }
}
