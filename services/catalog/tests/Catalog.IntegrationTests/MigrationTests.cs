using Catalog.Domain.Categories;
using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Catalog.IntegrationTests;

public sealed class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_creates_the_tree_tables_and_drops_the_skeleton_slug()
    {
        await using var db = postgres.CreateContext();
        var ct = TestContext.Current.CancellationToken;

        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' ORDER BY 1").ToListAsync(ct);
        Assert.Contains("categories", tables);
        Assert.Contains("attributes", tables);
        Assert.Contains("attribute_values", tables);
        Assert.Contains("category_attributes", tables);

        var slugColumns = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'categories' AND column_name = 'Slug'").ToListAsync(ct);
        Assert.Empty(slugColumns);
    }

    [Fact]
    public async Task Vietnamese_names_list_settings_and_ordered_values_survive_the_round_trip()
    {
        // NFR-L10N-003, and the list settings and value positions on the attribute (D-11).
        await using (var db = postgres.CreateContext())
        {
            db.Categories.Add(new Category(9001, null, 0, new LocalizedName("Thời trang", "Fashion"), true, "https://example.com/a.jpg"));
            db.Attributes.Add(new AttributeDefinition(9101, new LocalizedName("Màu sắc", "Color"), AttributeKind.List, true, 3, true));
            db.AttributeValues.AddRange(
                new AttributeValue(30, 9101, 0, new LocalizedName("Đỏ", "Red")),
                new AttributeValue(10, 9101, 1, new LocalizedName("Xanh", "Blue")),
                new AttributeValue(20, 9101, 2, new LocalizedName("Đen", "Black")));
            db.CategoryAttributes.Add(new CategoryAttribute(9001, 9101, 0, true));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = postgres.CreateContext();
        var category = await read.Categories.SingleAsync(c => c.Id == 9001, TestContext.Current.CancellationToken);
        var attribute = await read.Attributes.SingleAsync(a => a.Id == 9101, TestContext.Current.CancellationToken);
        var link = await read.CategoryAttributes.SingleAsync(l => l.CategoryId == 9001, TestContext.Current.CancellationToken);
        var values = await read.AttributeValues.Where(v => v.AttributeId == 9101).OrderBy(v => v.Position).Select(v => v.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Thời trang", category.Name.Vi);
        Assert.Equal("Fashion", category.Name.En);
        Assert.Equal((AttributeKind.List, true, 3, true), (attribute.Kind, attribute.AllowsMultiple, attribute.MaxValues, attribute.AllowsCustomValue));
        Assert.Equal((0, true), (link.Position, link.IsRequired));
        Assert.Equal([30, 10, 20], values);
    }

    [Fact]
    public async Task Multiple_choice_list_with_a_maximum_of_one_is_rejected()
    {
        await using var db = postgres.CreateContext();
        db.Attributes.Add(new AttributeDefinition(9102, new LocalizedName("B", "B"), AttributeKind.List, true, 1, false));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("CK_attributes_MaxValues", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Tac_9_a_text_attribute_cannot_accept_other_values()
    {
        await using var db = postgres.CreateContext();
        db.Attributes.Add(new AttributeDefinition(9103, new LocalizedName("Chất liệu", "Material"), AttributeKind.Text, false, 1, true));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("CK_attributes_AllowsCustomValue", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Tac_9_two_values_cannot_share_a_position_in_one_list()
    {
        await using var db = postgres.CreateContext();
        db.Attributes.Add(new AttributeDefinition(9104, new LocalizedName("Thương hiệu", "Brand"), AttributeKind.List, false, 1, false));
        db.AttributeValues.AddRange(new AttributeValue(9401, 9104, 0, new LocalizedName("A", "A")), new AttributeValue(9402, 9104, 0, new LocalizedName("B", "B")));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal("IX_attribute_values_AttributeId_Position", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [Fact]
    public async Task A_child_category_cannot_have_an_image()
    {
        await using var db = postgres.CreateContext();
        db.Categories.Add(new Category(9003, null, 0, new LocalizedName("Gốc", "Root"), false, null));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The entity drops the image for a child, so insert it with SQL to reach the database check.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            """INSERT INTO categories ("Id","ParentId","Position","IsLeaf","NameVi","NameEn","ImageUrl") VALUES (9004, 9003, 0, true, 'Con', 'Child', 'https://example.com/x.jpg')""",
            TestContext.Current.CancellationToken));
        Assert.Equal("CK_categories_ImageUrl_TopLevel", ex.ConstraintName);
    }

    [Fact]
    public async Task CategoryTree_migration_applies_down_to_Initial_and_up_again()
    {
        // A separate database so the shared one keeps its data for the other tests.
        var ct = TestContext.Current.CancellationToken;
        await using (var admin = postgres.CreateContext())
        {
            await admin.Database.ExecuteSqlRawAsync("CREATE DATABASE migration_roundtrip", ct);
        }

        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "migration_roundtrip" }.ConnectionString;
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(cancellationToken: ct);
        await migrator.MigrateAsync("20261117142656_Initial", ct);
        Assert.Equal(1L, await db.Database.SqlQueryRaw<long>("""SELECT count(*) AS "Value" FROM categories WHERE "Slug" = 'thoi-trang'""").SingleAsync(ct));

        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>("""SELECT count(*) AS "Value" FROM categories""").SingleAsync(ct));
    }

    [Fact]
    public async Task Tac_9_SharedAttributeLists_applies_down_to_CategoryTree_and_up_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var admin = postgres.CreateContext())
        {
            await admin.Database.ExecuteSqlRawAsync("CREATE DATABASE migration_shared_lists", ct);
        }

        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = "migration_shared_lists" }.ConnectionString;
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);
        var migrator = db.GetService<IMigrator>();
        async Task<List<string>> ColumnsAsync(string table) => await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = {0} ORDER BY 1", table).ToListAsync(ct);

        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Equal(["AttributeId", "CategoryId", "IsRequired", "Position"], await ColumnsAsync("category_attributes"));

        await migrator.MigrateAsync("20261126035151_CategoryTree", ct);
        Assert.Equal(["AllowsMultiple", "AttributeId", "CategoryId", "IsRequired", "Kind", "MaxValues", "Position", "ValueIds"], await ColumnsAsync("category_attributes"));
        Assert.DoesNotContain("Kind", await ColumnsAsync("attributes"));
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO categories ("Id","ParentId","Position","IsLeaf","NameVi","NameEn") VALUES (1, NULL, 0, true, 'A', 'A');
            INSERT INTO attributes ("Id","NameVi","NameEn") VALUES (2, 'B', 'B');
            INSERT INTO category_attributes ("CategoryId","AttributeId","Position","IsRequired","Kind","AllowsMultiple","MaxValues","ValueIds") VALUES (1, 2, 0, true, 'text', false, 1, ARRAY[]::integer[]);
            """,
            ct);

        // Up again empties the tree tables: rows in the old shape are dropped, and the operator reloads the file (design §4.1).
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Contains("AllowsCustomValue", await ColumnsAsync("attributes"));
        Assert.Contains("Position", await ColumnsAsync("attribute_values"));
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>("""SELECT count(*) AS "Value" FROM categories""").SingleAsync(ct));
        Assert.Equal(0L, await db.Database.SqlQueryRaw<long>("""SELECT count(*) AS "Value" FROM category_attributes""").SingleAsync(ct));
    }

    [Fact]
    public async Task Outbox_row_is_stored_with_its_envelope()
    {
        var envelope = EventEnvelope.Create("Ping", 1, "catalog", "test-correlation", new { message = "hi" });
        await using (var db = postgres.CreateContext())
        {
            db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = postgres.CreateContext();
        var stored = await read.Set<OutboxMessage>().SingleAsync(m => m.Id == envelope.EventId, TestContext.Current.CancellationToken);
        Assert.Null(stored.SentAt);
        Assert.Equal("Ping", EventEnvelope.FromJson(stored.Envelope).EventType);
    }
}
