using Catalog.Domain.Categories;
using Xunit;

namespace Catalog.UnitTests;

/// <summary>Builders for raw load files and the stored tree they produce.</summary>
internal static class CategoryTreeTestData
{
    public static RawName N(string vi, string? en = null) => new(vi, en ?? vi);

    public static RawCategory C(string key, string? parent, string name, string? image = null) => new(key, parent, N(name), image);

    public static RawValue V(string key, string name) => new(key, N(name));

    public static RawAttribute A(string key, string name, bool required, string input, int max, params RawValue[] values) =>
        new(key, N(name), required, input, max, values);

    public static RawCategoryAttributes Entry(string category, params RawAttribute[] attributes) => new(category, attributes);

    public static RawCategoryTree Tree(RawCategory[] categories, params RawCategoryAttributes[] entries) => new(categories, entries);

    /// <summary>Roots 1 (image) and 2; leaves 10, 11 under 1 and 20 under 2. Leaf 10 has Brand (single) and Color (multiple, max 5); leaf 20 has Material (text).</summary>
    public static RawCategoryTree Baseline() => Tree(
        [
            C("1", null, "Điện thoại", "https://example.com/1.jpg"),
            C("10", "1", "Smartphone"),
            C("11", "1", "Tablet"),
            C("2", null, "Nhà cửa"),
            C("20", "2", "Nồi"),
        ],
        Entry("10", A("100", "Thương hiệu", true, "Dropdown", 1, V("1000", "Apple"), V("1001", "Samsung")), A("101", "Màu", false, "Checkbox", 5, V("1100", "Đen"), V("1101", "Trắng"))),
        Entry("20", A("102", "Chất liệu", false, "Dropdown", 1)));

    public static CategoryTreeSource Build(RawCategoryTree raw)
    {
        var result = CategoryTreeSource.Build(raw);
        Assert.Empty(result.Problems);
        return result.Source!;
    }

    /// <summary>The stored tree as a previous load of <paramref name="source"/> would have left it.</summary>
    public static CurrentTree Stored(CategoryTreeSource source)
    {
        var first = source.Links.GroupBy(l => l.AttributeId).ToDictionary(g => g.Key, g => g.First());
        var order = source.Links.GroupBy(l => l.AttributeId).ToDictionary(g => g.Key, g => g.SelectMany(l => l.ValueIds).Distinct().ToList());
        return new(
            [.. source.Categories.Select(c => new Category(c.Id, c.ParentId, c.Position, c.Name, c.IsLeaf, c.ImageUrl))],
            [.. source.Attributes.Select(a => new AttributeDefinition(a.Id, a.Name, first[a.Id].Kind, first[a.Id].AllowsMultiple, first[a.Id].MaxValues, false))],
            [.. source.Values.Select(v => new AttributeValue(v.Id, v.AttributeId, order[v.AttributeId].IndexOf(v.Id), v.Name))],
            [.. source.Links.Select(l => new CategoryAttribute(l.CategoryId, l.AttributeId, l.Position, l.IsRequired))]);
    }

    public static IReadOnlySet<int> NoProducts { get; } = new HashSet<int>();
}
