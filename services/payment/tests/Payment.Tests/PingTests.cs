using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Payment.Tests;

public sealed class PingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    [InlineData("/v1/payment/ping")]
    public async Task Outer_shell_endpoints_answer(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
