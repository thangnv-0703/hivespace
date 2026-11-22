using System.Security.Cryptography;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Boots Identity.Api against a real Postgres container; the broker is faked (the relay is removed).</summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "http://localhost:5080";
    public const string SellerOrigin = "http://seller.localhost:5080";
    public const string ClientSecret = "test-client-secret";
    public const string SellerClientSecret = "test-seller-secret";

    /// <summary>The server's signing key, so tests can mint a token with a changed claim (wrong audience).</summary>
    public ECDsa SigningKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

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
        builder.UseSetting("Identity:Clients:Storefront:BaseUrl", Issuer);
        builder.UseSetting("Identity:Clients:Seller:BaseUrl", SellerOrigin);
        builder.UseSetting("Identity:SigningKeyPem", SigningKey.ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:EncryptionKeyPem", RSA.Create(2048).ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:Clients:Storefront:ClientSecret", ClientSecret);
        builder.UseSetting("Identity:Clients:Seller:ClientSecret", SellerClientSecret);
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
