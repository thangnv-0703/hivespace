using Catalog.Domain.Categories;
using Xunit;

namespace Catalog.UnitTests;

/// <summary>Builders for raw v2 load files (attributes.json plus link-only entries) and the stored tree they produce.</summary>
internal static class CategoryTreeTestData
{
    public static RawName N(string vi, string? en = null) => new(vi, en ?? vi);

    public static RawCategory C(string key, string? parent, string name, string? image = null) => new(key, parent, N(name), image);

    public static RawValue V(string key, string name) => new(key, N(name));

    /// <summary>An attribute of attributes.json; it accepts no other values unless built with <see cref="Open"/>.</summary>
    public static RawAttributeDefinition D(string key, string name, string input, int max, params RawValue[] values) =>
        new(key, N(name), input, max, false, values);

    /// <summary>An open list: the file marks it as accepting other values (FR-CAT-022).</summary>
    public static RawAttributeDefinition Open(RawAttributeDefinition definition) => definition with { AllowsCustomValue = true };

    public static RawLink L(string key, bool required = false) => new(key, required);

    public static RawCategoryAttributes Entry(string category, params RawLink[] links) => new(category, links);

    public static RawCategoryTree Tree(RawCategory[] categories, RawAttributeDefinition[] attributes, params RawCategoryAttributes[] entries) =>
        new(categories, attributes, entries);

    public static RawCategoryTree Tree(RawCategory[] categories) => new(categories, [], []);

    public static RawCategory[] BaselineCategories() =>
    [
        C("1", null, "Điện thoại", "https://example.com/1.jpg"),
        C("10", "1", "Smartphone"),
        C("11", "1", "Tablet"),
        C("2", null, "Nhà cửa"),
        C("20", "2", "Nồi"),
    ];

    public static RawAttributeDefinition Brand() => D("100", "Thương hiệu", "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung"));

    public static RawAttributeDefinition Color() => D("101", "Màu", "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"));

    public static RawAttributeDefinition Material() => D("102", "Chất liệu", "Dropdown", 1);

    /// <summary>Roots 1 (image) and 2; leaves 10, 11 under 1 and 20 under 2. Leaf 10 links Brand (single, required) and Color (multiple, max 5); leaf 20 links Material (text).</summary>
    public static RawCategoryTree Baseline() => Tree(
        BaselineCategories(),
        [Brand(), Color(), Material()],
        Entry("10", L("100", true), L("101")),
        Entry("20", L("102")));

    /// <summary>The baseline categories and links with other attribute definitions.</summary>
    public static RawCategoryTree WithAttributes(params RawAttributeDefinition[] attributes) => Baseline() with { Attributes = attributes };

    public static CategoryTreeSource Build(RawCategoryTree raw)
    {
        var result = CategoryTreeSource.Build(raw);
        Assert.Empty(result.Problems);
        return result.Source!;
    }

    /// <summary>The stored tree as a previous load of <paramref name="source"/> would have left it.</summary>
    public static CurrentTree Stored(CategoryTreeSource source) => new(
        [.. source.Categories.Select(c => new Category(c.Id, c.ParentId, c.Position, c.Name, c.IsLeaf, c.ImageUrl))],
        [.. source.Attributes.Select(a => new AttributeDefinition(a.Id, a.Name, a.Kind, a.AllowsMultiple, a.MaxValues, a.AllowsCustomValue))],
        [.. source.Values.Select(v => new AttributeValue(v.Id, v.AttributeId, v.Position, v.Name))],
        [.. source.Links.Select(l => new CategoryAttribute(l.CategoryId, l.AttributeId, l.Position, l.IsRequired))]);

    public static IReadOnlySet<int> NoProducts { get; } = new HashSet<int>();
}
