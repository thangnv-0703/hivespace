using System.Security.Cryptography;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.ContractTests;

/// <summary>Boots Identity.Api against a real Postgres container; the broker is faked (the relay is removed).</summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "http://localhost:5080";
    public const string ClientSecret = "test-client-secret";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async Task StartAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:identitydb", _postgres.GetConnectionString());
        // A syntactically valid emulator string; the outbox relay is removed below so nothing connects.
        builder.UseSetting(
            "ConnectionStrings:messaging",
            "Endpoint=sb://localhost;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;");
        // Fresh keys per run; production reads them from configuration (D-7).
        builder.UseSetting("Identity:Issuer", Issuer);
        builder.UseSetting("Identity:PublicBaseUrl", Issuer);
        builder.UseSetting("Identity:SigningKeyPem", ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:EncryptionKeyPem", RSA.Create(2048).ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:Gateway:ClientSecret", ClientSecret);
        builder.ConfigureServices(services =>
        {
            var relay = services.Single(d => d.ImplementationType?.Name.StartsWith("OutboxRelay", StringComparison.Ordinal) == true);
            services.Remove(relay);
            services.RemoveAll<IEventBus>();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>The shell endpoints every service answers (moved here from the template ping test).</summary>
public sealed class ShellTests : IAsyncLifetime
{
    private IdentityApiFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory();
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    public async Task Outer_shell_endpoints_answer(string path)
    {
        using var client = _factory!.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
