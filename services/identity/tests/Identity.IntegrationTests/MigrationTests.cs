using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Identity.IntegrationTests;

public sealed class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "users", "credentials", "role_grants", "verification_tokens", "outbox", "inbox",
        "OpenIddictApplications", "OpenIddictAuthorizations", "OpenIddictScopes", "OpenIddictTokens",
    ];

    [Fact]
    public async Task Up_creates_every_table_the_design_lists()
    {
        await using var db = postgres.CreateContext();

        var tables = await ListTables(db);

        foreach (var table in ExpectedTables)
        {
            Assert.Contains(table, tables);
        }
    }

    [Fact]
    public async Task Email_is_unique_on_the_normalized_address()
    {
        await using var db = postgres.CreateContext();
        var user = $"{Guid.CreateVersion7()}";
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
            values ({Guid.CreateVersion7()}, {"A@x.com"}, {user}, {"A"}, {"vi"}, now(), now())
            """,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
            values ({Guid.CreateVersion7()}, {"a@x.com"}, {user}, {"B"}, {"vi"}, now(), now())
            """,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Down_drops_everything_and_up_applies_again()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("0", TestContext.Current.CancellationToken);
        var afterDown = await ListTables(db);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var afterUp = await ListTables(db);

        Assert.DoesNotContain("users", afterDown);
        Assert.DoesNotContain("OpenIddictTokens", afterDown);
        Assert.Contains("users", afterUp);
    }

    private static async Task<List<string>> ListTables(DbContext db) =>
        await db.Database
            .SqlQuery<string>($"select table_name as \"Value\" from information_schema.tables where table_schema = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken);
}
