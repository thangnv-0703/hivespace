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
        Assert.Equal(4, result.Counts.ListValues.Unchanged);
        Assert.Equal(new ImageCounts(0, 0, 0, 1), result.Counts.Images);
    }

    [Fact]
    public void Growth_is_applied_and_counted()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("10", "1", "Smartphone"), C("11", "1", "Tablet"), C("12", "1", "Mới"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung"), V("1002", "Xiaomi")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("12", A("103", "Size", true, "Dropdown", 1, V("1300", "S"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        Assert.Equal(1, result.Counts.Categories.Added);
        Assert.Equal(1, result.Counts.Leaves.Added);
        Assert.Equal(1, result.Counts.Attributes.Added);
        Assert.Equal(2, result.Counts.ListValues.Added);
        Assert.Equal([1002], result.Changes!.LinkUpdates.Single().ValueIds.Except([1000, 1001]));
    }

    [Fact]
    public void Renames_change_names_only()
    {
        var file = Tree(
            [C("1", null, "Điện thoại & Tablet", "https://example.com/1.jpg"), C("10", "1", "Smartphone"), C("11", "1", "Tablet"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            Entry("10", A("100", "Hãng", true, "Dropdown", 1, V("1000", "Apple Inc"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        var changes = result.Changes!;
        Assert.Equal([1], changes.CategoryRenames.Select(r => r.Id));
        Assert.Equal([100], changes.AttributeRenames.Select(r => r.Id));
        Assert.Equal([1000], changes.ValueRenames.Select(r => r.Id));
        Assert.Empty(changes.CategoryRepositions);
        Assert.Empty(changes.LinkUpdates);
        Assert.Empty(changes.NewLinks);
        Assert.Equal((1, 1, 1), (result.Counts.Categories.Renamed, result.Counts.Attributes.Renamed, result.Counts.ListValues.Renamed));
    }

    [Fact]
    public void Each_loosening_is_applied_on_its_own()
    {
        TreeDiffResult Loosen(RawAttribute brand, RawAttribute color) => Diff(
            Baseline(),
            Tree(Baseline().Categories.ToArray(), Entry("10", brand, color), Entry("20", A("102", "Chất liệu", false, "Dropdown", 1))));

        var optional = Loosen(A("100", "Thương hiệu", false, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng")));
        var multiple = Loosen(A("100", "Thương hiệu", true, "Checkbox", 3, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng")));
        var higher = Loosen(A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 8, V("1100", "Đen"), V("1101", "Trắng")));

        foreach (var r in new[] { optional, multiple, higher })
        {
            Assert.False(r.IsRefused);
            Assert.Equal(1, r.Counts.Attributes.Loosened);
            Assert.Single(r.Changes!.LinkUpdates);
        }

        Assert.False(optional.Changes!.LinkUpdates.Single().IsRequired);
        Assert.True(multiple.Changes!.LinkUpdates.Single().AllowsMultiple);
        Assert.Equal(8, higher.Changes!.LinkUpdates.Single().MaxValues);
    }

    [Fact]
    public void Sibling_order_follows_the_file()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("11", "1", "Tablet"), C("10", "1", "Smartphone"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        var result = Diff(Baseline(), file);

        Assert.False(result.IsRefused);
        Assert.Equal(2, result.Changes!.CategoryRepositions.Count);
        Assert.Equal(2, result.Counts.Categories.Reordered);
    }

    [Fact]
    public void Every_refusal_rule_is_listed_together()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("10", "2", "Smartphone"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi")],
            Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple")), A("101", "Màu", true, "Dropdown", 1, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1, V("1200", "Gỗ"))));

        var codes = Codes(Diff(Baseline(), file));

        Assert.Contains("category.removed", codes);
        Assert.Contains("category.moved", codes);
        Assert.Contains("value.removed", codes);
        Assert.Contains("attribute.tightened", codes);
        Assert.Equal(3, codes.Count(c => c == "attribute.tightened"));
        Assert.Equal(6, codes.Length);
    }

    [Fact]
    public void An_omitted_attribute_is_a_problem()
    {
        var file = Tree(Baseline().Categories.ToArray(), Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung"))), Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        Assert.Equal(["attribute.removed"], Codes(Diff(Baseline(), file)));
    }

    [Fact]
    public void Changing_between_list_and_free_text_is_tightening()
    {
        var toText = Tree(Baseline().Categories.ToArray(), Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))), Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));
        var toList = Tree(Baseline().Categories.ToArray(), Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))), Entry("20", A("102", "Chất liệu", false, "Dropdown", 1, V("1200", "Gỗ"))));

        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), toText)));
        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), toList)));
    }

    [Fact]
    public void Multiple_to_single_and_a_lower_maximum_are_tightening()
    {
        RawCategoryTree With(RawAttribute color) => Tree(Baseline().Categories.ToArray(), Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), color), Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), With(A("101", "Màu", false, "Dropdown", 1, V("1100", "Đen"), V("1101", "Trắng"))))));
        Assert.Equal(["attribute.tightened"], Codes(Diff(Baseline(), With(A("101", "Màu", false, "Checkbox", 3, V("1100", "Đen"), V("1101", "Trắng"))))));
    }

    [Fact]
    public void A_child_under_a_category_with_products_is_refused_and_otherwise_makes_it_a_non_leaf()
    {
        var file = Tree(
            [C("1", null, "Điện thoại", "https://example.com/1.jpg"), C("10", "1", "Smartphone"), C("11", "1", "Tablet"), C("2", null, "Nhà cửa"), C("20", "2", "Nồi"), C("200", "20", "Nồi cơm")],
            Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

        var refused = Diff(Baseline(), file, 20);
        var accepted = Diff(Baseline(), file);

        Assert.Equal(["category.child_under_used_leaf"], Codes(refused));
        Assert.Contains("20", refused.Problems.Single().Detail.Replace("parent ", string.Empty).Split(' '));
        Assert.False(accepted.IsRefused);
        Assert.Equal([(20, false)], accepted.Changes!.LeafChanges);
    }

    [Fact]
    public void A_new_required_attribute_on_a_category_with_products_is_refused_but_optional_is_fine()
    {
        RawCategoryTree With(bool required) => Tree(
            Baseline().Categories.ToArray(),
            Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
            Entry("20", A("102", "Chất liệu", false, "Dropdown", 1), A("103", "Xuất xứ", required, "Dropdown", 1, V("1300", "Việt Nam"))));

        Assert.Equal(["attribute.required_on_used_category"], Codes(Diff(Baseline(), With(true), 20)));
        Assert.False(Diff(Baseline(), With(false), 20).IsRefused);
        Assert.False(Diff(Baseline(), With(true)).IsRefused);
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
