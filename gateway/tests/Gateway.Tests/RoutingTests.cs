using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Gateway.Tests;

/// <summary>Routes /v1/categories to the catalog cluster and owns the identity headers (FR-AUTH-016).</summary>
public sealed class RoutingTests : IClassFixture<GatewayTestHost>
{
    private readonly GatewayTestHost _host;

    public RoutingTests(GatewayTestHost host) => _host = host;

    [Fact]
    public async Task Anonymous_route_forwards_with_correlation_and_never_a_client_supplied_identity()
    {
        using var client = _host.NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/categories");
        request.Headers.Add("X-User-Id", "spoofed");
        request.Headers.Add("X-User-Roles", "Seller");
        request.Headers.Add("Authorization", "Bearer spoofed");
        request.Headers.Add("X-Correlation-ID", "corr-123");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var echoed = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        // The dev-user stub is gone: with no session nothing is asserted on the user's behalf.
        Assert.Equal(string.Empty, echoed.GetProperty("userId").GetString());
        Assert.Equal(string.Empty, echoed.GetProperty("roles").GetString());
        Assert.Equal(string.Empty, echoed.GetProperty("authorization").GetString());
        Assert.Equal("corr-123", echoed.GetProperty("correlationId").GetString());
    }
}
