using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gateway.Tests;

/// <summary>Routes /v1/categories to the catalog cluster and owns the identity headers (FR-AUTH-016).</summary>
public sealed class RoutingTests : IAsyncLifetime
{
    private WebApplication? _echo;
    private string _echoUrl = string.Empty;

    public async ValueTask InitializeAsync()
    {
        // A stand-in for Catalog that echoes the headers it received.
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _echo = builder.Build();
        _echo.MapGet("/v1/categories", (HttpRequest r) => new
        {
            userId = r.Headers["X-User-Id"].ToString(),
            correlationId = r.Headers["X-Correlation-ID"].ToString(),
        });
        await _echo.StartAsync();
        _echoUrl = _echo.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_echo is not null)
        {
            await _echo.DisposeAsync();
        }
    }

    [Fact]
    public async Task Forwards_categories_with_gateway_owned_identity_and_correlation_headers()
    {
        await using var gateway = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ReverseProxy:Clusters:catalog:Destinations:d1:Address", _echoUrl);
        });
        using var client = gateway.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/categories");
        request.Headers.Add("X-User-Id", "spoofed");
        request.Headers.Add("X-Correlation-ID", "corr-123");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var echoed = await response.Content.ReadFromJsonAsync<Echo>(TestContext.Current.CancellationToken);
        Assert.Equal("dev-user", echoed!.UserId);
        Assert.Equal("corr-123", echoed.CorrelationId);
    }

    private sealed record Echo(string UserId, string CorrelationId);
}
