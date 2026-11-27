using Catalog.Domain.Categories;
using Xunit;
using static Catalog.UnitTests.CategoryTreeTestData;

namespace Catalog.UnitTests;

public sealed class CategoryTreeDiffTests
{
    private static TreeDiffResult Diff(RawCategoryTree stored, RawCategoryTree file, params int[] used) =>
        CategoryTreeDiff.Compute(Stored(Build(stored)), Build(file), new HashSet<int>(used));

    private static string[] Codes(TreeDiffResult r) => [.. r.Problems.Select(p => p.Code)];

    [Fact]
    public void An_empty_store_gets_everything_parents_first()
    {
        var result = CategoryTreeDiff.Compute(new CurrentTree([], [], [], []), Build(Baseline()), NoProducts);

        var changes = result.Changes!;
        Assert.Equal(5, changes.NewCategories.Count);
        Assert.True(changes.NewCategories.ToList().FindIndex(c => c.Id == 1) < changes.NewCategories.ToList().FindIndex(c => c.Id == 10));
        Assert.Equal(3, changes.NewAttributes.Count);
        Assert.Equal(4, changes.NewValues.Count);
        Assert.Equal(3, changes.NewLinks.Count);
        Assert.Equal(5, result.Counts.Categories.Added);
        Assert.Equal(new ImageCounts(1, 0, 0, 0), result.Counts.Images);
    }

    [Fact]
    public void The_same_file_again_changes_nothing()
    {
        var result = Diff(Baseline(), Baseline());

        Assert.False(result.IsRefused);
        Assert.True(result.Changes!.IsEmpty);
        Assert.Equal(5, result.Counts.Categories.Unchanged);
        Assert.Equal(3, result.Counts.Attributes.Unchanged);
        Assert.Equal(3, result.Counts.AttributeLinks.Unchanged);
        Assert.Equal(4, result.Counts.ListValues.Unchanged);
        Assert.Equal(new ImageCounts(0, 0, 0, 1), result.Counts.Images);
    }

    [Fact]
    public void Growth_is_applied_and_counted()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("10", "1", "Smartphone"), C("11", "1", "Tablet"), C("12", "1", "Mới"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            [D("100", "Thương hiệu", "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung"), V("1002", "Xiaomi")), Color(), Material(), D("103", "Size", "Dropdown", 1, V("1300", "S"))],
            Entry("10", L("100", true), L("101")),
            Entry("12", L("103", true)),
            Entry("20", L("102")));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        Assert.Equal(1, result.Counts.Categories.Added);
        Assert.Equal(1, result.Counts.Leaves.Added);
        Assert.Equal(1, result.Counts.Attributes.Added);
        Assert.Equal(1, result.Counts.AttributeLinks.Added);
        Assert.Equal(2, result.Counts.ListValues.Added);
        var xiaomi = Assert.Single(result.Changes!.NewValues, v => v.Id == 1002);
        Assert.Equal((100, 2), (xiaomi.AttributeId, xiaomi.Position));
        Assert.Empty(result.Changes.ValueRepositions);
    }

    [Fact]
    public void Renames_change_names_only()
    {
        var file = Tree(
            [C("1", null, "Điện thoại & Tablet", "https://example.com/1.jpg"), C("10", "1", "Smartphone"), C("11", "1", "Tablet"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            [D("100", "Hãng", "Dropdown", 1, V("1000", "Apple Inc"), V("1001", "Samsung")), Color(), Material()],
            Entry("10", L("100", true), L("101")),
            Entry("20", L("102")));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        var changes = result.Changes!;
        Assert.Equal([1], changes.CategoryRenames.Select(r => r.Id));
        Assert.Equal([100], changes.AttributeRenames.Select(r => r.Id));
        Assert.Equal([1000], changes.ValueRenames.Select(r => r.Id));
        Assert.Empty(changes.CategoryRepositions);
        Assert.Empty(changes.AttributeUpdates);
        Assert.Empty(changes.LinkUpdates);
        Assert.Empty(changes.ValueRepositions);
        Assert.Empty(changes.NewLinks);
        Assert.Equal((1, 1, 1), (result.Counts.Categories.Renamed, result.Counts.Attributes.Renamed, result.Counts.ListValues.Renamed));
    }

    [Fact]
    public void Ac_2_3_each_loosening_is_applied_on_its_own()
    {
        // Required → optional is set on the link; the other three on the attribute (D-11).
        var optional = Diff(Baseline(), Tree(BaselineCategories(), [Brand(), Color(), Material()], Entry("10", L("100"), L("101")), Entry("20", L("102"))));
        var multiple = Diff(Baseline(), WithAttributes(D("100", "Thương hiệu", "Checkbox", 3, V("1000", "Apple"), V("1001", "Samsung")), Color(), Material()));
        var higher = Diff(Baseline(), WithAttributes(Brand(), D("101", "Màu", "Checkbox", 8, V("1100", "Đen"), V("1101", "Trắng")), Material()));
        var opened = Diff(Baseline(), WithAttributes(Open(Brand()), Color(), Material()));

        Assert.False(optional.IsRefused);
        Assert.Equal(1, optional.Counts.AttributeLinks.Loosened);
        Assert.False(Assert.Single(optional.Changes!.LinkUpdates).IsRequired);
        foreach (var r in new[] { multiple, higher, opened })
        {
            Assert.False(r.IsRefused);
            Assert.Equal(1, r.Counts.Attributes.Loosened);
            Assert.Single(r.Changes!.AttributeUpdates);
            Assert.Empty(r.Changes.LinkUpdates);
        }

        Assert.True(multiple.Changes!.AttributeUpdates.Single().AllowsMultiple);
        Assert.Equal(8, higher.Changes!.AttributeUpdates.Single().MaxValues);
        Assert.Equal((100, true), (opened.Changes!.AttributeUpdates.Single().Id, opened.Changes.AttributeUpdates.Single().AllowsCustomValue));
    }

    [Fact]
    public void Sibling_order_follows_the_file()
    {
        var file = Baseline() with
        {
            Categories = [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("11", "1", "Tablet"), C("10", "1", "Smartphone"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
        };

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        Assert.Equal(2, result.Changes!.CategoryRepositions.Count);
        Assert.Equal(2, result.Counts.Categories.Reordered);
    }

    [Fact]
    public void Value_and_link_order_follow_the_file()
    {
        var file = Tree(
            BaselineCategories(),
            [D("100", "Thương hiệu", "Dropdown", 1, V("1002", "Xiaomi"), V("1000", "Apple"), V("1001", "Samsung")), Color(), Material()],
            Entry("10", L("101"), L("100", true)),
            Entry("20", L("102")));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        Assert.Equal([(1000, 1), (1001, 2)], result.Changes!.ValueRepositions);
        Assert.Equal((0, 1), (result.Changes.NewValues.Single().Position, result.Counts.ListValues.Added));
        Assert.Equal(2, result.Counts.ListValues.Reordered);
        Assert.Equal(2, result.Counts.AttributeLinks.Reordered);
    }

    [Fact]
    public void Every_refusal_rule_is_listed_together()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("10", "2", "Smartphone"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            [D("100", "Thương hiệu", "Dropdown", 1, V("1000", "Apple")), D("101", "Màu", "Dropdown", 1, V("1100", "Đen"), V("1101", "Trắng")), D("102", "Chất liệu", "Dropdown", 1, V("1200", "Gỗ"))],
            Entry("10", L("100", true), L("101", true)),
            Entry("20", L("102")));

        var codes = Codes(Diff(Baseline(), file));

        Assert.Contains("category.removed", codes);
        Assert.Contains("category.moved", codes);
        Assert.Contains("value.removed", codes);
        Assert.Contains("attribute.tightened", codes);
        Assert.Equal(3, codes.Count(c => c == "attribute.tightened"));
        Assert.Equal(6, codes.Length);
    }

    [Fact]
    public void An_omitted_link_is_a_problem()
    {
        var file = Tree(BaselineCategories(), [Brand(), Color(), Material()], Entry("10", L("100", true)), Entry("20", L("102")));

        var problem = Assert.Single(Diff(Baseline(), file).Problems);
        Assert.Equal(("attribute.removed", "101", 10), (problem.Code, problem.Key, problem.CategoryId));
    }

    [Fact]
    public void Ac_2_4_an_omitted_attribute_definition_is_a_problem()
    {
        var file = Tree(BaselineCategories(), [Brand(), Material()], Entry("10", L("100", true)), Entry("20", L("102")));

        var problems = Diff(Baseline(), file).Problems;

        Assert.Equal(["attribute.removed", "attribute.removed"], problems.Select(p => p.Code));
        Assert.Contains(problems, p => p.Key == "101" && p.CategoryId is null && p.Detail.Contains("attributes.json", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Key == "101" && p.CategoryId == 10);
    }

    [Fact]
    public void An_omitted_value_is_a_problem()
    {
        var problem = Assert.Single(Diff(Baseline(), WithAttributes(D("100", "Thương hiệu", "Dropdown", 1, V("1000", "Apple")), Color(), Material())).Problems);

        Assert.Equal(("value.removed", "1001"), (problem.Code, problem.Key));
    }

    [Fact]
    public void Optional_to_required_on_a_link_is_tightening()
    {
        var file = Tree(BaselineCategories(), [Brand(), Color(), Material()], Entry("10", L("100", true), L("101", true)), Entry("20", L("102")));

        var problem = Assert.Single(Diff(Baseline(), file).Problems);
        Assert.Equal(("attribute.tightened", 10, "optional → required"), (problem.Code, problem.CategoryId, problem.Detail));
    }

    [Fact]
    public void Changing_between_list_and_free_text_is_tightening()
    {
        var toText = WithAttributes(D("100", "Thương hiệu", "Dropdown", 1), Color(), Material());
        var toList = WithAttributes(Brand(), Color(), D("102", "Chất liệu", "Dropdown", 1, V("1200", "Gỗ")));

        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), toText)));
        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), toList)));
    }

    [Fact]
    public void Multiple_to_single_and_a_lower_maximum_are_tightening()
    {
        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), WithAttributes(Brand(), D("101", "Màu", "Dropdown", 1, V("1100", "Đen"), V("1101", "Trắng")), Material()))));
        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), WithAttributes(Brand(), D("101", "Màu", "Checkbox", 3, V("1100", "Đen"), V("1101", "Trắng")), Material()))));
    }

    [Fact]
    public void Ac_2_4_a_list_that_stops_accepting_other_values_is_tightening()
    {
        var open = WithAttributes(Open(Brand()), Color(), Material());

        var problem = Assert.Single(Diff(open, Baseline()).Problems);

        Assert.Equal(("attribute.tightened", "100", "accepts other values → does not"), (problem.Code, problem.Key, problem.Detail));
    }

    [Fact]
    public void A_child_under_a_category_with_products_is_refused_and_otherwise_makes_it_a_non_leaf()
    {
        var file = Baseline() with
        {
            Categories = [.. BaselineCategories(), C("200", "20", "Nồi cơm")],
        };

        var refused = Diff(Baseline(), file, 20);
        var accepted = Diff(Baseline(), file);

        Assert.Equal(["category.child_under_used_leaf"], Codes(refused));
        Assert.Contains("20", refused.Problems.Single().Detail.Replace("parent ", string.Empty).Split(' '));
        Assert.False(accepted.IsRefused);
        Assert.Equal([(20, false)], accepted.Changes!.LeafChanges);
    }

    [Fact]
    public void Ac_2_7_a_new_required_attribute_on_a_category_with_products_is_refused_but_optional_is_fine()
    {
        RawCategoryTree With(bool required) => Tree(
            BaselineCategories(),
            [Brand(), Color(), Material(), D("103", "Xuất xứ", "Dropdown", 1, V("1300", "Việt Nam"))],
            Entry("10", L("100", true), L("101")),
            Entry("20", L("102"), L("103", required)));

        Assert.Equal(["attribute.required_on_used_category"], Codes(Diff(Baseline(), With(true), 20)));
        Assert.False(Diff(Baseline(), With(false), 20).IsRefused);
        Assert.False(Diff(Baseline(), With(true)).IsRefused);
    }

    // Root 1 → parent 2 (links A 100 required, B 101) → middle 3 (no links) → leaf 4 (no links), and leaf 5 under 2.
    private static RawCategory[] InheritCategories() =>
        [C("1", null, "Gốc"), C("2", "1", "Cha"), C("3", "2", "Giữa"), C("4", "3", "Lá"), C("5", "2", "Lá khác")];

    private static RawAttributeDefinition[] InheritAttributes() =>
        [Brand(), Color(), D("102", "Chất liệu", "Dropdown", 1), D("103", "Xuất xứ", "Dropdown", 1, V("1300", "Việt Nam"))];

    private static RawCategoryTree Inherit(params RawCategoryAttributes[] entries) => Tree(InheritCategories(), InheritAttributes(), entries);

    private static RawCategoryTree InheritBaseline() => Inherit(Entry("2", L("100", true), L("101")));

    [Fact]
    public void Ac_2_7_a_new_required_attribute_on_a_parent_a_used_leaf_inherits_is_refused_but_optional_is_fine()
    {
        var required = Inherit(Entry("2", L("100", true), L("101"), L("103", true)));
        var optional = Inherit(Entry("2", L("100", true), L("101"), L("103")));

        var problem = Assert.Single(Diff(InheritBaseline(), required, 4).Problems);
        Assert.Equal(("attribute.required_on_used_category", "103", 4), (problem.Code, problem.Key, problem.CategoryId));
        Assert.Contains("inherited from category 2", problem.Detail, StringComparison.Ordinal);
        Assert.False(Diff(InheritBaseline(), optional, 4).IsRefused);
    }

    [Fact]
    public void Ac_2_8_own_links_that_leave_out_an_inherited_attribute_are_refused()
    {
        var ownSubset = Inherit(Entry("2", L("100", true), L("101")), Entry("4", L("100", true)));

        var problem = Assert.Single(Diff(InheritBaseline(), ownSubset, 4).Problems);

        Assert.Equal(("attribute.inherited_dropped", "101", 4), (problem.Code, problem.Key, problem.CategoryId));
        Assert.False(Diff(InheritBaseline(), ownSubset).IsRefused);
    }

    [Fact]
    public void Ac_2_8_links_on_a_nearer_ancestor_that_leave_out_an_inherited_attribute_are_refused()
    {
        var nearer = Inherit(Entry("2", L("100", true), L("101")), Entry("3", L("100", true)));

        var problem = Assert.Single(Diff(InheritBaseline(), nearer, 4).Problems);

        Assert.Equal(("attribute.inherited_dropped", "101", 4), (problem.Code, problem.Key, problem.CategoryId));
        Assert.Contains("from category 3 instead of 2", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac_2_9_own_links_with_every_inherited_attribute_are_applied_unless_one_more_is_required()
    {
        var superset = Inherit(Entry("2", L("100", true), L("101")), Entry("4", L("100", true), L("101"), L("103")));
        var supersetRequired = Inherit(Entry("2", L("100", true), L("101")), Entry("4", L("100", true), L("101"), L("103", true)));

        var applied = Diff(InheritBaseline(), superset, 4);

        Assert.False(applied.IsRefused);
        Assert.Equal(3, applied.Changes!.NewLinks.Count(l => l.CategoryId == 4));
        var problem = Assert.Single(Diff(InheritBaseline(), supersetRequired, 4).Problems);
        Assert.Equal(("attribute.required_on_used_category", "103", 4), (problem.Code, problem.Key, problem.CategoryId));
    }

    [Theory]
    [InlineData("4")] // the used leaf gets its own links
    [InlineData("3")] // a nearer ancestor gets links
    public void Ac_2_9_an_inherited_optional_attribute_that_becomes_required_for_a_used_leaf_is_refused(string provider)
    {
        var file = Inherit(Entry("2", L("100", true), L("101")), Entry(provider, L("100", true), L("101", true)));

        var problem = Assert.Single(Diff(InheritBaseline(), file, 4).Problems);

        Assert.Equal(("attribute.tightened", "101", 4), (problem.Code, problem.Key, problem.CategoryId));
        Assert.False(Diff(InheritBaseline(), file).IsRefused);
    }

    [Fact]
    public void Ac_2_9_a_flip_to_required_on_the_same_provider_is_reported_once()
    {
        var file = Inherit(Entry("2", L("100", true), L("101", true)));

        var problem = Assert.Single(Diff(InheritBaseline(), file, 4).Problems);

        Assert.Equal(("attribute.tightened", "101", 2), (problem.Code, problem.Key, problem.CategoryId));
    }

    [Fact]
    public void Images_can_be_added_changed_and_removed()
    {
        var stored = Tree([C("1", null, "A", "https://example.com/1.jpg"), C("2", null, "B", "https://example.com/2.jpg"), C("3", null, "C"), C("4", null, "D", "https://example.com/4.jpg")]);
        var file = Tree([C("1", null, "A", "https://example.com/1b.jpg"), C("2", null, "B"), C("3", null, "C", "https://example.com/3.jpg"), C("4", null, "D", "https://example.com/4.jpg")]);

        var result = Diff(stored, file);

        Assert.False(result.IsRefused);
        Assert.Equal(new ImageCounts(1, 1, 1, 1), result.Counts.Images);
        Assert.Equal(3, result.Changes!.ImageChanges.Count);
        Assert.Contains((2, (string?)null), result.Changes.ImageChanges);
    }

    [Fact]
    public void An_image_dropped_below_the_top_level_is_not_a_change()
    {
        var file = Tree([C("1", null, "A"), C("10", "1", "B", "https://example.com/x.jpg")]);

        Assert.True(Diff(Tree([C("1", null, "A"), C("10", "1", "B")]), file).Changes!.IsEmpty);
    }
}
