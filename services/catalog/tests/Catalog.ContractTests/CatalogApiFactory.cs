using Catalog.Application.Abstractions;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Catalog.ContractTests;

/// <summary>Boots Catalog.Api against a real Postgres container; Inventory and the broker are faked at their ports.</summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public async Task StartAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:catalogdb", _postgres.GetConnectionString());
        // A syntactically valid emulator string; the outbox relay is removed below so nothing connects.
        builder.UseSetting(
            "ConnectionStrings:messaging",
            "Endpoint=sb://localhost;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInventoryClient>();
            services.AddScoped<IInventoryClient, StubInventory>();
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

    private sealed class StubInventory : IInventoryClient
    {
        public Task<string> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult("stub");
    }
}
