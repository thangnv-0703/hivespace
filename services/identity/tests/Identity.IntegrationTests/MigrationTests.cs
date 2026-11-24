using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Identity.IntegrationTests;

public sealed class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] ExpectedTables =
    [
        "users", "credentials", "role_grants", "verification_tokens", "external_logins", "sign_in_lockouts", "audit_entries", "sign_in_code_challenges", "outbox", "inbox",
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
    public async Task AddExternalLogins_applies_down_and_up_after_AddUserProfile()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261120171033_AddUserProfile", TestContext.Current.CancellationToken);
        var before = await ListTables(db);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var after = await ListTables(db);

        Assert.DoesNotContain("external_logins", before);
        Assert.Contains("users", before);
        Assert.Contains("external_logins", after);
    }

    [Fact]
    public async Task AddLockoutAndAudit_applies_down_and_up_after_AddExternalLogins()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261123022324_AddExternalLogins", TestContext.Current.CancellationToken);
        var before = await ListTables(db);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var after = await ListTables(db);

        Assert.DoesNotContain("sign_in_lockouts", before);
        Assert.DoesNotContain("audit_entries", before);
        Assert.Contains("sign_in_lockouts", after);
        Assert.Contains("audit_entries", after);
    }

    [Fact]
    public async Task Lockout_and_audit_checks_reject_values_outside_the_design()
    {
        await using var db = postgres.CreateContext();
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
            values ({id}, {"l@x.com"}, {"l@x.com"}, {"L"}, {"vi"}, now(), now())
            """,
            ct);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into sign_in_lockouts (user_id, failed_count, window_started_at) values ({id}, {6}, now())", ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, method) values ({Guid.CreateVersion7()}, now(), {"other"}, {"password"})", ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, method) values ({Guid.CreateVersion7()}, now(), {"sign_in_failed"}, {"sms"})", ct));
    }

    [Fact]
    public async Task AddSignInCodeChallenges_applies_down_and_up_after_AddLockoutAndAudit()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();
        var ct = TestContext.Current.CancellationToken;

        await migrator.MigrateAsync("20261123075254_AddLockoutAndAudit", ct);
        var before = await ListTables(db);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, method) values ({Guid.CreateVersion7()}, now(), {"sign_in_failed"}, {"code"})", ct));
        await migrator.MigrateAsync(cancellationToken: ct);
        var after = await ListTables(db);

        Assert.DoesNotContain("sign_in_code_challenges", before);
        Assert.Contains("sign_in_code_challenges", after);
        // FEAT-006: method 'code' is now accepted.
        await db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, method) values ({Guid.CreateVersion7()}, now(), {"sign_in_failed"}, {"code"})", ct);
        await db.Database.ExecuteSqlAsync($"delete from audit_entries where method = 'code'", ct);
    }

    [Fact]
    public async Task AddShops_applies_up_down_and_up_after_AddSignInCodeChallenges()
    {
        await using var db = postgres.CreateContext();
        var migrator = db.GetService<IMigrator>();
        var ct = TestContext.Current.CancellationToken;

        await migrator.MigrateAsync("20261124013009_AddSignInCodeChallenges", ct);
        var before = await ListTables(db);
        await migrator.MigrateAsync(cancellationToken: ct);
        var after = await ListTables(db);

        Assert.DoesNotContain("shops", before);
        Assert.Contains("shops", after);

        // Seller state, then down: the shops, the Seller grants and the seller audit rows go; the Buyer grant stays.
        var user = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
            values ({user}, {"s@x.com"}, {"s@x.com"}, {"S"}, {"vi"}, now(), now())
            """,
            ct);
        await db.Database.ExecuteSqlAsync($"insert into role_grants (user_id, role, granted_at) values ({user}, {"Buyer"}, now()), ({user}, {"Seller"}, now())", ct);
        await db.Database.ExecuteSqlAsync(
            $"insert into shops (id, owner_user_id, name, normalized_name, seller_type, created_at, updated_at) values ({Guid.CreateVersion7()}, {user}, {"Shop"}, {"shop"}, {"individual"}, now(), now())",
            ct);
        await db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, user_id) values ({Guid.CreateVersion7()}, now(), {"seller_role_granted"}, {user})", ct);

        await migrator.MigrateAsync("20261124013009_AddSignInCodeChallenges", ct);
        var roles = await db.Database.SqlQuery<string>($"select role as \"Value\" from role_grants where user_id = {user}").ToListAsync(ct);
        var audit = await db.Database.SqlQuery<string>($"select type as \"Value\" from audit_entries where user_id = {user}").ToListAsync(ct);
        Assert.DoesNotContain("shops", await ListTables(db));
        Assert.Equal(["Buyer"], roles);
        Assert.Empty(audit);

        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Contains("shops", await ListTables(db));
    }

    [Fact]
    public async Task Shops_check_the_seller_type_and_the_audit_method_may_be_null_for_a_role_grant()
    {
        await using var db = postgres.CreateContext();
        var ct = TestContext.Current.CancellationToken;
        var user = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
            values ({user}, {"t@x.com"}, {"t@x.com"}, {"T"}, {"vi"}, now(), now())
            """,
            ct);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into shops (id, owner_user_id, name, normalized_name, seller_type, created_at, updated_at) values ({Guid.CreateVersion7()}, {user}, {"Shop"}, {"shop-t"}, {"other"}, now(), now())",
            ct));
        await db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, user_id) values ({Guid.CreateVersion7()}, now(), {"seller_role_granted"}, {user})", ct);
        // Other types still need a method.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into audit_entries (id, occurred_at, type, method) values ({Guid.CreateVersion7()}, now(), {"seller_role_granted"}, {"sms"})", ct));
    }

    [Fact]
    public async Task Sign_in_code_challenges_allow_one_open_challenge_per_address_and_check_their_values()
    {
        await using var db = postgres.CreateContext();
        var ct = TestContext.Current.CancellationToken;
        var emailHash = Guid.CreateVersion7().ToByteArray();
        byte[] codeHash = [1, 2, 3];

        await db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, created_at, expires_at) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, now(), now())",
            ct);
        // A second open challenge for the same address.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, created_at, expires_at) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, now(), now())",
            ct));
        // An ended one is fine.
        await db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, created_at, expires_at, ended_at, end_reason) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, now(), now(), now(), {"used"})",
            ct);
        // Values outside the design.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, failed_count, created_at, expires_at, ended_at, end_reason) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, {6}, now(), now(), now(), {"attempts"})",
            ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, created_at, expires_at, ended_at, end_reason) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, now(), now(), now(), {"other"})",
            ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into sign_in_code_challenges (id, email_hash, code_hash, created_at, expires_at, ended_at) values ({Guid.CreateVersion7()}, {emailHash}, {codeHash}, now(), now(), now())",
            ct));
    }

    [Fact]
    public async Task External_logins_are_unique_per_subject_and_per_user_and_provider()
    {
        await using var db = postgres.CreateContext();
        var ct = TestContext.Current.CancellationToken;
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        foreach (var id in new[] { first, second })
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                insert into users (id, email, normalized_email, display_name, language, created_at, updated_at)
                values ({id}, {id + "@x.com"}, {id + "@x.com"}, {"E"}, {"vi"}, now(), now())
                """,
                ct);
        }

        var subject = Guid.NewGuid().ToString("N");
        await db.Database.ExecuteSqlAsync(
            $"insert into external_logins (id, user_id, provider, subject, linked_at) values ({Guid.CreateVersion7()}, {first}, {"google"}, {subject}, now())",
            ct);

        // The same Google identity on another account.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into external_logins (id, user_id, provider, subject, linked_at) values ({Guid.CreateVersion7()}, {second}, {"google"}, {subject}, now())",
            ct));

        // A second Google identity on the same account.
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into external_logins (id, user_id, provider, subject, linked_at) values ({Guid.CreateVersion7()}, {first}, {"google"}, {"other"}, now())",
            ct));
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
