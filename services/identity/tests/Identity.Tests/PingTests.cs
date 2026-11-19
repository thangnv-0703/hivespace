using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Identity.Tests;

public sealed class PingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    // The outer shell needs no database; FEAT-001 T6 moves these checks onto the Postgres-backed contract factory.
    public PingTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Database:MigrateOnStartup", "false");
            b.UseSetting("ConnectionStrings:identitydb", "Host=unused;Database=unused");
            b.UseSetting(
                "ConnectionStrings:messaging",
                "Endpoint=sb://localhost;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;");
        });

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    [InlineData("/v1/identity/ping")]
    public async Task Outer_shell_endpoints_answer(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
