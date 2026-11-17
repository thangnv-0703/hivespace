using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Npgsql;
using Xunit;

namespace HiveSpace.SystemTests;

/// <summary>The walking skeleton's done-when, API half (docs/implementation-plan.md §2).</summary>
public sealed class SkeletonTests(StackFixture stack)
{
    [Fact]
    public async Task Every_service_answers_health_through_the_stack()
    {
        foreach (var service in StackFixture.Services)
        {
            using var client = stack.App.CreateHttpClient(service);
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{service} /health returned {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Categories_round_trip_gateway_catalog_postgres_and_inventory_grpc()
    {
        using var gateway = stack.App.CreateHttpClient("gateway");

        using var body = JsonDocument.Parse(
            await gateway.GetStringAsync(new Uri("/v1/categories", UriKind.Relative), TestContext.Current.CancellationToken));

        var category = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray(),
            c => c.GetProperty("slug").GetString() == "thoi-trang");
        Assert.Equal("Thời trang", category.GetProperty("name").GetProperty("vi").GetString());
        // The probe only comes back if Catalog reached Inventory over gRPC.
        Assert.Equal("inventory-ok", body.RootElement.GetProperty("availabilityProbe").GetString());
    }

    [Fact]
    public async Task Ping_event_reaches_go_and_python_inboxes_once()
    {
        using var gateway = stack.App.CreateHttpClient("gateway");
        using var response = await gateway.PostAsync(new Uri("/v1/catalog/ping", UriKind.Relative), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var eventId = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("eventId").GetGuid();

        foreach (var database in new[] { "inventorydb", "notificationdb" })
        {
            var count = await WaitForInboxRowAsync(database, eventId);
            Assert.True(count == 1, $"{database}: expected 1 inbox row for {eventId}, found {count}");
        }
    }

    private async Task<long> WaitForInboxRowAsync(string database, Guid eventId)
    {
        var connectionString = await stack.App.GetConnectionStringAsync(database, TestContext.Current.CancellationToken);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM inbox WHERE event_id = @id", connection);
            command.Parameters.AddWithValue("id", eventId);
            var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
            if (count > 0 || DateTime.UtcNow > deadline)
            {
                return count;
            }

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }
    }
}
