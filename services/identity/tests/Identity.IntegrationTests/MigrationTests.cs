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

    [Fact]
    public async Task AddUserProfile_applies_down_and_up_after_the_initial_migration()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261119151119_InitialIdentity", TestContext.Current.CancellationToken);
        var before = await ListUserColumns(db);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var after = await ListUserColumns(db);

        Assert.DoesNotContain("phone", before);
        Assert.DoesNotContain("gender", before);
        Assert.DoesNotContain("date_of_birth", before);
        Assert.Contains("phone", after);
        Assert.Contains("gender", after);
        Assert.Contains("date_of_birth", after);
    }

    [Fact]
    public async Task Gender_is_limited_to_the_four_values()
    {
        await using var db = postgres.CreateContext();

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, gender, created_at, updated_at)
            values ({Guid.CreateVersion7()}, {"g@x.com"}, {"g@x.com"}, {"G"}, {"vi"}, {"x"}, now(), now())
            """,
            TestContext.Current.CancellationToken));
    }

    private static async Task<List<string>> ListUserColumns(DbContext db) =>
        await db.Database
            .SqlQuery<string>($"select column_name as \"Value\" from information_schema.columns where table_schema = 'public' and table_name = 'users'")
            .ToListAsync(TestContext.Current.CancellationToken);

    private static async Task<List<string>> ListTables(DbContext db) =>
        await db.Database
            .SqlQuery<string>($"select table_name as \"Value\" from information_schema.tables where table_schema = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken);
}
