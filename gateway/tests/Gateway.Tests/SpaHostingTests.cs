using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gateway.Tests;

/// <summary>ADR-0027: in the production image each SPA is served on its own host, deep links included.</summary>
public sealed class SpaHostingTests : IAsyncLifetime
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "hs-gateway-wwwroot-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program>? _gateway;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // The layout the Dockerfile produces: storefront at the root, seller centre in wwwroot/seller, both with /assets.
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "seller", "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "storefront-index");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "storefront-js");
        File.WriteAllText(Path.Combine(_webRoot, "seller", "index.html"), "seller-index");
        File.WriteAllText(Path.Combine(_webRoot, "seller", "assets", "app.js"), "seller-js");

        _gateway = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseWebRoot(_webRoot);
            b.UseSetting("Identity:Issuer", GatewayTestHost.Issuer);
            b.UseSetting("Identity:Clients:Seller:BaseUrl", GatewayTestHost.SellerOrigin);
        });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_gateway is not null)
        {
            await _gateway.DisposeAsync();
        }

        Directory.Delete(_webRoot, recursive: true);
    }

    [Theory]
    [InlineData(GatewayTestHost.Issuer, "/", "storefront-index")]
    [InlineData(GatewayTestHost.Issuer, "/cart", "storefront-index")]
    [InlineData(GatewayTestHost.Issuer, "/assets/app.js", "storefront-js")]
    [InlineData(GatewayTestHost.SellerOrigin, "/", "seller-index")]
    [InlineData(GatewayTestHost.SellerOrigin, "/products/42", "seller-index")]
    [InlineData(GatewayTestHost.SellerOrigin, "/assets/app.js", "seller-js")]
    public async Task Each_host_serves_its_own_app(string origin, string path, string expected)
    {
        using var client = _gateway!.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(origin), AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(Ct));
    }
}
