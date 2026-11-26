using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>A real Postgres in a container (dev-standards §9.3: no hand-written database mocks).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as the AppHost (pgvector on Postgres 17).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public CatalogDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>A new empty migrated database in the same container, so a test can load into a clean tree.</summary>
    public async Task<string> NewDatabaseAsync()
    {
        var name = "t" + Guid.NewGuid().ToString("N");
        await using (var admin = CreateContext())
        {
#pragma warning disable EF1003 // the name is a generated hex id, not user input
            await admin.Database.ExecuteSqlRawAsync("CREATE DATABASE " + name);
#pragma warning restore EF1003
        }

        var connection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync();
        return connection;
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
