using System.Text.Json;
using System.Text.Json.Nodes;
using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;
using Catalog.Infrastructure.CategorySource;
using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Messaging;
using Json.Schema;
using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>The category load against a real Postgres (FEAT-014 T4): AC-014-1.x, 2.x, 5.x and TAC-014-1..3.</summary>
public sealed class CategoryLoadTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "category-tree", name);

    private sealed class Rig(string connection, Func<ICategoryTreeStore, ICategoryTreeStore>? wrapStore = null) : IAsyncDisposable
    {
        public CatalogDbContext Db { get; } = new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(connection).Options);

        public Task<LoadReport> LoadAsync(string fixture) => LoadFromAsync(Fixture(fixture));

        public Task<LoadReport> LoadFromAsync(string sourcePath)
        {
            ICategoryTreeStore store = new EfCategoryTreeStore(Db);
            if (wrapStore is not null)
            {
                store = wrapStore(store);
            }

            var handler = new LoadCategoryTreeHandler(new JsonCategoryTreeSourceReader(), store, new EmptyCategoryUsage(), new TestOutbox(Db), TimeProvider.System);
            return handler.HandleAsync(sourcePath, Ct);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    /// <summary>The Catalog adapter is internal, so tests stage the same envelope through their own copy.</summary>
    private sealed class TestOutbox(CatalogDbContext db) : IEventOutbox
    {
        public Guid Stage<T>(string eventType, int version, string correlationId, T data)
        {
            var envelope = EventEnvelope.Create(eventType, version, "catalog", correlationId, data);
            db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
            return envelope.EventId;
        }

        public Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private async Task<(string Connection, Rig Rig)> NewRigAsync()
    {
        var connection = await postgres.NewDatabaseAsync();
        return (connection, new Rig(connection));
    }

    private static async Task<int> OutboxCountAsync(string connection)
    {
        await using var rig = new Rig(connection);
        return await rig.Db.Set<OutboxMessage>().CountAsync(m => m.EventType == "CategoryTreeChanged", Ct);
    }

    private static async Task<(int Categories, int Attributes, int Values, int Links)> RowCountsAsync(string connection)
    {
        await using var rig = new Rig(connection);
        return (await rig.Db.Categories.CountAsync(Ct), await rig.Db.Attributes.CountAsync(Ct), await rig.Db.AttributeValues.CountAsync(Ct), await rig.Db.CategoryAttributes.CountAsync(Ct));
    }

    [Fact]
    public async Task Ac_1_1_first_load_stores_the_tree_with_the_file_keys_names_and_order()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;

        var report = await rig.LoadAsync("valid-v1");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.Equal((17, 7, 24, 19), await RowCountsAsync(connection) is var c ? (c.Categories, c.Attributes, c.Values, c.Links) : default);
        await using var read = new Rig(connection);
        var categories = await read.Db.Categories.AsNoTracking().ToDictionaryAsync(x => x.Id, Ct);
        Assert.Equal(("Điện Thoại - Máy Tính Bảng", "Phones & Tablets", null), (categories[1789].Name.Vi, categories[1789].Name.En, categories[1789].ParentId));
        Assert.Equal((1789, 0), (categories[1795].ParentId!.Value, categories[1795].Position));
        Assert.Equal(1, categories[1794].Position);
        Assert.Equal(0, categories[8062].Position);
        Assert.Equal(1, categories[8063].Position);
        Assert.False(categories[1789].IsLeaf);
        Assert.True(categories[1795].IsLeaf);
        Assert.True(await read.Db.CategoryAttributes.AnyAsync(l => l.CategoryId == 1795 && l.AttributeId == 1003, Ct));
        var color = await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1003, Ct);
        Assert.Equal((AttributeKind.List, true, 3), (color.Kind, color.AllowsMultiple, color.MaxValues));
        Assert.Equal([5201, 5202, 5203, 5204], await read.Db.AttributeValues.Where(v => v.AttributeId == 1003).OrderBy(v => v.Position).Select(v => v.Id).ToListAsync(Ct));
        Assert.True(await read.Db.CategoryAttributes.AnyAsync(l => l.CategoryId == 8062 && l.AttributeId == 1005, Ct));
        var material = await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1005, Ct);
        Assert.Equal(AttributeKind.Text, material.Kind);
        Assert.False(await read.Db.AttributeValues.AnyAsync(v => v.AttributeId == 1005, Ct));
        Assert.Equal(1, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_1_2_the_report_counts_added_and_unchanged_items()
    {
        var (_, rig) = await NewRigAsync();
        await using var _r = rig;

        var first = await rig.LoadAsync("valid-v1");
        var grow = await rig.LoadAsync("v2-grow");

        Assert.Equal(17, first.Counts.Categories.Added);
        Assert.Equal(11, first.Counts.Leaves.Added);
        Assert.Equal(new CountRow(1, 0, 0, 0, 17, 18), grow.Counts.Categories);
        Assert.Equal(1, grow.Counts.Leaves.Added);
        Assert.Equal(1, grow.Counts.Attributes.Added);
    }

    [Fact]
    public async Task Ac_1_3_names_missing_refuses_the_load_and_changes_nothing()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;

        var report = await rig.LoadAsync("invalid-names");

        Assert.Equal(LoadOutcome.Refused, report.Outcome);
        Assert.Equal(["name.missing", "name.missing", "name.missing"], report.Problems.Select(p => p.Code));
        Assert.Equal((0, 0, 0, 0), await RowCountsAsync(connection) is var c ? (c.Categories, c.Attributes, c.Values, c.Links) : default);
        Assert.Equal(0, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_1_4_and_2_4_forbidden_changes_are_all_listed_and_nothing_changes()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");
        var before = await RowCountsAsync(connection);

        var report = await rig.LoadAsync("invalid-changes");

        Assert.Equal(LoadOutcome.Refused, report.Outcome);
        var codes = report.Problems.Select(p => p.Code).ToHashSet();
        Assert.Superset(new HashSet<string> { "category.removed", "category.moved", "attribute.removed", "value.removed", "attribute.tightened" }, codes);
        Assert.Equal(before, await RowCountsAsync(connection));
        Assert.Equal(1, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Malformed_json_is_reported_with_file_and_position()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;

        var report = await rig.LoadAsync("malformed-json");

        Assert.Equal(LoadOutcome.Refused, report.Outcome);
        var problem = Assert.Single(report.Problems);
        Assert.Equal("file.malformed", problem.Code);
        Assert.Contains("categories.json", problem.Key);
        Assert.Contains("line", problem.Detail);
        Assert.Equal(0, (await RowCountsAsync(connection)).Categories);
    }

    [Fact]
    public async Task A_missing_source_directory_is_a_file_problem()
    {
        var (_, rig) = await NewRigAsync();
        await using var _r = rig;

        var report = await new LoadCategoryTreeHandler(new JsonCategoryTreeSourceReader(), new EfCategoryTreeStore(rig.Db), new EmptyCategoryUsage(), new TestOutbox(rig.Db), TimeProvider.System)
            .HandleAsync(Path.Combine(AppContext.BaseDirectory, "no-such-directory"), Ct);

        Assert.Equal("file.unreadable", Assert.Single(report.Problems).Code);
    }

    [Fact]
    public async Task Ac_1_5_loading_the_same_file_again_changes_nothing_and_writes_no_second_event()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");
        var before = await RowCountsAsync(connection);

        var again = await rig.LoadAsync("valid-v1");

        Assert.Equal(LoadOutcome.Unchanged, again.Outcome);
        Assert.Equal(17, again.Counts.Categories.Unchanged);
        Assert.Equal(before, await RowCountsAsync(connection));
        Assert.Equal(1, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_2_1_growth_adds_rows_and_writes_a_second_event()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");

        var report = await rig.LoadAsync("v2-grow");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        await using var read = new Rig(connection);
        Assert.NotNull(await read.Db.Categories.FindAsync([2352], Ct));
        Assert.NotNull(await read.Db.AttributeValues.FindAsync([5205], Ct));
        Assert.NotNull(await read.Db.Attributes.FindAsync([1008], Ct));
        Assert.Equal(2, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_2_2_a_rename_changes_names_only()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");
        var before = await RowCountsAsync(connection);

        var report = await rig.LoadAsync("v2-rename");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        await using var read = new Rig(connection);
        var category = await read.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1789, Ct);
        Assert.Equal(("Điện Thoại - Máy Tính Bảng", "Phones and Tablets"), (category.Name.Vi, category.Name.En));
        Assert.Equal("Brand name", (await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1001, Ct)).Name.En);
        Assert.Equal("Apple Inc", (await read.Db.AttributeValues.AsNoTracking().SingleAsync(v => v.Id == 5001, Ct)).Name.Vi);
        Assert.Equal(before, await RowCountsAsync(connection));
    }

    [Fact]
    public async Task Ac_2_3_loosening_is_stored()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");

        var report = await rig.LoadAsync("v2-loosen");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.True(report.Counts.Attributes.Loosened > 0);
        await using var read = new Rig(connection);
        Assert.False((await read.Db.CategoryAttributes.AsNoTracking().SingleAsync(l => l.CategoryId == 1795 && l.AttributeId == 1001, Ct)).IsRequired);
        var storage = await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1002, Ct);
        Assert.Equal((true, 3), (storage.AllowsMultiple, storage.MaxValues));
    }

    [Fact]
    public async Task Ac_2_6_a_leaf_without_products_can_gain_a_child()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");

        var report = await rig.LoadAsync("v2-leaf-gains-child");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        await using var read = new Rig(connection);
        Assert.False((await read.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1883, Ct)).IsLeaf);
        Assert.True((await read.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1884, Ct)).IsLeaf);
    }

    [Fact]
    public async Task Ac_5_1_and_5_3_top_level_images_are_stored_changed_and_removed()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;

        await rig.LoadAsync("valid-v1");
        await using (var read = new Rig(connection))
        {
            Assert.Equal("https://example.com/sample-images/categories/phones-tablets.jpg", (await read.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1789, Ct)).ImageUrl);
            Assert.Null((await read.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1686, Ct)).ImageUrl);
        }

        var report = await rig.LoadAsync("v2-images");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.Equal(new ImageCounts(1, 1, 1, 0), report.Counts.Images);
        await using var after = new Rig(connection);
        Assert.EndsWith("phones-tablets-v2.jpg", (await after.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1789, Ct)).ImageUrl);
        Assert.Null((await after.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 8322, Ct)).ImageUrl);
        Assert.EndsWith("mens-fashion.jpg", (await after.Db.Categories.AsNoTracking().SingleAsync(c => c.Id == 1686, Ct)).ImageUrl);
    }

    [Fact]
    public async Task Tac_1_a_failure_after_the_rows_are_written_leaves_nothing()
    {
        var connection = await postgres.NewDatabaseAsync();
        await using var failing = new Rig(connection, inner => new FailingStore(inner));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.LoadAsync("valid-v1"));

        Assert.Equal((0, 0, 0, 0), await RowCountsAsync(connection) is var c ? (c.Categories, c.Attributes, c.Values, c.Links) : default);
        Assert.Equal(0, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Tac_2_the_outbox_row_matches_the_event_schema()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");

        var message = await rig.Db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == "CategoryTreeChanged", Ct);

        var envelope = EventEnvelope.FromJson(message.Envelope);
        Assert.Equal(("CategoryTreeChanged", 1, "catalog"), (envelope.EventType, envelope.EventVersion, envelope.Producer));
        var schema = JsonSchema.FromText(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Schemas", "CategoryTreeChanged.v1.schema.json"), Ct));
        using var document = JsonDocument.Parse(message.Envelope);
        Assert.True(schema.Evaluate(document.RootElement.GetProperty("data")).IsValid, "event data does not match the schema");
        Assert.Equal(1, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Tac_3_two_loads_at_once_run_one_after_the_other()
    {
        var connection = await postgres.NewDatabaseAsync();
        await using var one = new Rig(connection);
        await using var two = new Rig(connection);

        var reports = await Task.WhenAll(one.LoadAsync("valid-v1"), two.LoadAsync("valid-v1"));

        Assert.Equal([LoadOutcome.Applied, LoadOutcome.Unchanged], reports.Select(r => r.Outcome).Order());
        Assert.Equal(1, await OutboxCountAsync(connection));
        Assert.Equal(17, (await RowCountsAsync(connection)).Categories);
    }

    [Fact]
    public async Task Ac_2_3_opening_a_list_is_stored()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");

        var report = await rig.LoadAsync("v2-open-list");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.Equal(1, report.Counts.Attributes.Loosened);
        await using var read = new Rig(connection);
        Assert.True((await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1001, Ct)).AllowsCustomValue);
        Assert.Equal(2, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_2_4_closing_a_list_is_refused_and_nothing_changes()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("v2-open-list");
        var before = await RowCountsAsync(connection);

        var report = await rig.LoadAsync("v2-close-list");

        Assert.Equal(LoadOutcome.Refused, report.Outcome);
        var problem = Assert.Single(report.Problems);
        Assert.Equal(("attribute.tightened", "1001", "accepts other values → does not"), (problem.Code, problem.Key, problem.Detail));
        await using var read = new Rig(connection);
        Assert.True((await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1001, Ct)).AllowsCustomValue);
        Assert.Equal(before, await RowCountsAsync(connection));
        Assert.Equal(1, await OutboxCountAsync(connection));
    }

    [Fact]
    public async Task Ac_2_9_a_leaf_that_inherits_can_get_own_links_with_every_inherited_attribute()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("inherit");

        var report = await rig.LoadAsync("inherit-own-superset");

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.Equal(3, report.Counts.AttributeLinks.Added);
        await using var read = new Rig(connection);
        Assert.Equal([1001, 1010, 1011], await read.Db.CategoryAttributes.Where(l => l.CategoryId == 7076).OrderBy(l => l.Position).Select(l => l.AttributeId).ToListAsync(Ct));
        Assert.False(await read.Db.CategoryAttributes.AnyAsync(l => l.CategoryId == 7077, Ct));
    }

    [Fact]
    public async Task A_leaf_without_products_may_get_own_links_that_leave_out_an_inherited_attribute()
    {
        // With products this is attribute.inherited_dropped (AC-014-2.8, unit-tested); FEAT-012 brings products.
        var (_, rig) = await NewRigAsync();
        await using var _r = rig;
        await rig.LoadAsync("inherit");

        Assert.Equal(LoadOutcome.Applied, (await rig.LoadAsync("inherit-own-subset")).Outcome);
    }

    [Fact]
    public async Task Values_that_swap_positions_are_stored_in_the_new_order()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");
        var reordered = CopyFixture("valid-v1", dir => EditJson(Path.Combine(dir, "attributes.json"), root =>
        {
            var values = Attribute(root, "1001")["values"]!.AsArray();
            var reversed = values.Select(v => v!.DeepClone()).Reverse().ToList();
            values.Clear();
            reversed.ForEach(values.Add);
        }));

        var report = await rig.LoadFromAsync(reordered);

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        Assert.Equal(4, report.Counts.ListValues.Reordered);
        await using var read = new Rig(connection);
        Assert.Equal([5004, 5003, 5002, 5001], await read.Db.AttributeValues.Where(v => v.AttributeId == 1001).OrderBy(v => v.Position).Select(v => v.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task Tac_10_links_without_attributes_json_are_each_unknown()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        var source = CopyFixture("valid-v1", dir => File.Delete(Path.Combine(dir, "attributes.json")));

        var report = await rig.LoadFromAsync(source);

        Assert.Equal(LoadOutcome.Refused, report.Outcome);
        Assert.Equal(19, report.Problems.Count);
        Assert.All(report.Problems, p => Assert.Equal("attributes.unknown", p.Code));
        Assert.Equal(0, (await RowCountsAsync(connection)).Categories);
    }

    [Fact]
    public async Task Tac_10_a_non_boolean_allowsCustomValue_is_malformed_with_file_and_item()
    {
        var (_, rig) = await NewRigAsync();
        await using var _r = rig;
        var source = CopyFixture("valid-v1", dir => EditJson(Path.Combine(dir, "attributes.json"), root => Attribute(root, "1002")["allowsCustomValue"] = "yes"));

        var problem = Assert.Single((await rig.LoadFromAsync(source)).Problems);

        Assert.Equal(("file.malformed", "attributes.json item 1"), (problem.Code, problem.Key));
        Assert.Contains("allowsCustomValue", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tac_10_an_attribute_defined_twice_is_a_duplicate_key()
    {
        var (_, rig) = await NewRigAsync();
        await using var _r = rig;
        var source = CopyFixture("valid-v1", dir => EditJson(Path.Combine(dir, "attributes.json"), root =>
            root["attributes"]!.AsArray().Add(Attribute(root, "1005").DeepClone())));

        var problem = Assert.Single((await rig.LoadFromAsync(source)).Problems);

        Assert.Equal(("attribute.duplicate_key", "1005"), (problem.Code, problem.Key));
    }

    [Fact]
    public async Task Tac_10_old_format_fields_on_a_link_are_ignored()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        var source = CopyFixture("valid-v1", dir => EditJson(Path.Combine(dir, "category-attributes", "01-phones-accessories.json"), root =>
        {
            var link = root["categories"]![0]!["attributes"]![0]!.AsObject();
            link["name"] = new JsonObject { ["vi"] = "Khác", ["en"] = "Other" };
            link["inputType"] = "Checkbox";
            link["maxValueCount"] = 9;
            link["values"] = new JsonArray(new JsonObject { ["sourceValueId"] = "9999", ["name"] = new JsonObject { ["vi"] = "X", ["en"] = "X" } });
        }));

        var report = await rig.LoadFromAsync(source);

        Assert.Equal(LoadOutcome.Applied, report.Outcome);
        await using var read = new Rig(connection);
        var brand = await read.Db.Attributes.AsNoTracking().SingleAsync(a => a.Id == 1001, Ct);
        Assert.Equal(("Thương hiệu", false, 1), (brand.Name.Vi, brand.AllowsMultiple, brand.MaxValues));
        Assert.Null(await read.Db.AttributeValues.FindAsync([9999], Ct));
        Assert.Equal((17, 7, 24, 19), await RowCountsAsync(connection) is var c ? (c.Categories, c.Attributes, c.Values, c.Links) : default);
    }

    [Fact]
    public async Task Ac_2_6_the_former_leaf_shows_no_attributes_and_its_new_child_inherits_them()
    {
        var (connection, rig) = await NewRigAsync();
        await using var _ = rig;
        await rig.LoadAsync("valid-v1");
        var grown = CopyFixture("valid-v1", dir => EditJson(Path.Combine(dir, "categories.json"), root => root["categories"]!.AsArray().Add(new JsonObject
        {
            ["externalCategoryId"] = "1799",
            ["externalParentCategoryId"] = "1795",
            ["name"] = new JsonObject { ["vi"] = "Điện thoại 5G", ["en"] = "5G phones" },
        })));

        Assert.Equal(LoadOutcome.Applied, (await rig.LoadFromAsync(grown)).Outcome);

        // Read through the service's own registration of the read port (the adapter is internal).
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:catalogdb"] = connection;
        builder.Configuration["ConnectionStrings:messaging"] = "Endpoint=sb://localhost;SharedAccessKeyName=unused;SharedAccessKey=dW51c2Vk;UseDevelopmentEmulator=true";
        builder.AddCatalogInfrastructure();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICategoryReadStore>();

        var former = (await store.GetDetailAsync(1795, Ct))!;
        Assert.False(former.Category.IsLeaf);
        Assert.Empty(former.Attributes);
        var child = (await store.GetDetailAsync(1799, Ct))!;
        Assert.True(child.Category.IsLeaf);
        Assert.Equal([1001, 1002, 1003], child.Attributes.Select(a => a.Definition.Id));
        Assert.All(child.Attributes, a => Assert.Equal(1795, a.Link.CategoryId));
        Assert.Equal([5001, 5002, 5003, 5004], child.Attributes[0].Values.Select(v => v.Id));
        Assert.Equal([1001, 1002, 1003], (await store.GetEffectiveAttributesAsync(1799, Ct)).Select(a => a.Definition.Id));
        Assert.Empty(await store.GetEffectiveAttributesAsync(1795, Ct));
    }

    /// <summary>A copy of a fixture in a fresh temporary directory, changed by <paramref name="change"/>.</summary>
    private static string CopyFixture(string name, Action<string> change)
    {
        var target = Path.Combine(Path.GetTempPath(), "category-tree-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(Fixture(name), "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(target, Path.GetRelativePath(Fixture(name), file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
        }

        change(target);
        return target;
    }

    private static void EditJson(string path, Action<JsonNode> edit)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        edit(root);
        File.WriteAllText(path, root.ToJsonString());
    }

    private static JsonObject Attribute(JsonNode root, string key) =>
        root["attributes"]!.AsArray().Single(a => (string?)a!["sourceAttributeId"] == key)!.AsObject();

    private sealed class FailingStore(ICategoryTreeStore inner) : ICategoryTreeStore
    {
        public Task<T> RunLoadAsync<T>(Func<ICategoryTreeLoad, Task<LoadStep<T>>> work, CancellationToken cancellationToken) =>
            inner.RunLoadAsync(load => work(new FailingLoad(load)), cancellationToken);

        private sealed class FailingLoad(ICategoryTreeLoad inner) : ICategoryTreeLoad
        {
            public Task<CurrentTree> LoadCurrentAsync(CancellationToken cancellationToken) => inner.LoadCurrentAsync(cancellationToken);

            public async Task ApplyAsync(ChangeSet changes, CancellationToken cancellationToken)
            {
                await inner.ApplyAsync(changes, cancellationToken);
                throw new InvalidOperationException("the store failed after the first batch");
            }
        }
    }
}
