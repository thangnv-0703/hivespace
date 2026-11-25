using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>A real Postgres in a container (dev-standards §9.3: no hand-written database mocks).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as the AppHost (pgvector on Postgres 17).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public IdentityDbContext CreateContext(Microsoft.Extensions.Logging.ILoggerProvider? logs = null) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString)
            .UseOpenIddict()
            .UseLoggerFactory(logs is null ? null : new Microsoft.Extensions.Logging.LoggerFactory([logs]))
            .Options);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
