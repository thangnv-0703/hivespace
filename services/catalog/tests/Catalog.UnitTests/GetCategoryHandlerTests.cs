using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Abstractions;
using Xunit;

namespace Catalog.UnitTests;

public sealed class GetCategoryHandlerTests
{
    private static readonly Category Root = new(1, null, 0, new LocalizedName("Gốc", "Root"), false, null);
    private static readonly Category Leaf = new(10, 1, 0, new LocalizedName("Lá", "Leaf"), true, null);

    private static async Task<Result<CategoryDetailDto>> Get(CategoryDetailData? data) =>
        await new GetCategoryHandler(new FakeStore(data)).HandleAsync(10, TestContext.Current.CancellationToken);

    [Fact]
    public async Task An_unknown_category_is_not_found() =>
        Assert.Equal(ErrorCodes.NotFound, (await Get(null)).Failure!.Code);

    [Fact]
    public async Task A_list_attribute_returns_its_selection_maximum_and_ordered_values()
    {
        var link = new CategoryAttribute(10, 100, 0, true);
        var data = new CategoryDetailData(
            Leaf, [Root],
            [new AttributeData(link, new AttributeDefinition(100, new LocalizedName("Màu", "Color"), AttributeKind.List, true, 3, false), [new AttributeValue(2, 100, 0, new LocalizedName("Đỏ", "Red")), new AttributeValue(1, 100, 1, new LocalizedName("Samsung", "Samsung"))])]);

        var detail = (await Get(data)).Value!;

        Assert.Equal([1], detail.Path.Select(p => p.Id));
        var attribute = Assert.Single(detail.Attributes);
        Assert.Equal(("list", "multiple", 3, true, false), (attribute.Kind, attribute.Selection, attribute.MaxValues, attribute.Required, attribute.AllowsCustomValue));
        Assert.Equal([2, 1], attribute.Values.Select(v => v.Id));
        Assert.Equal(("Samsung", "Samsung"), (attribute.Values[1].Name.Vi, attribute.Values[1].Name.En));
    }

    [Fact]
    public async Task A_text_attribute_has_no_selection_maximum_or_values()
    {
        var link = new CategoryAttribute(10, 101, 0, false);
        var data = new CategoryDetailData(Leaf, [Root], [new AttributeData(link, new AttributeDefinition(101, new LocalizedName("Chất liệu", "Material"), AttributeKind.Text, false, 1, false), [])]);

        var attribute = Assert.Single((await Get(data)).Value!.Attributes);

        Assert.Equal(("text", null, null, false), (attribute.Kind, attribute.Selection, attribute.MaxValues, attribute.AllowsCustomValue));
        Assert.Empty(attribute.Values);
    }

    [Fact]
    public async Task Ac_4_6_an_open_list_says_it_accepts_other_values_and_keeps_its_values_in_order()
    {
        var link = new CategoryAttribute(10, 100, 0, true);
        var brand = new AttributeDefinition(100, new LocalizedName("Thương hiệu", "Brand"), AttributeKind.List, false, 1, true);
        var data = new CategoryDetailData(Leaf, [Root], [new AttributeData(link, brand, [new AttributeValue(3, 100, 0, new LocalizedName("Apple", "Apple")), new AttributeValue(1, 100, 1, new LocalizedName("Sony", "Sony"))])]);

        var attribute = Assert.Single((await Get(data)).Value!.Attributes);

        Assert.Equal(("list", "single", 1, true), (attribute.Kind, attribute.Selection, attribute.MaxValues, attribute.AllowsCustomValue));
        Assert.Equal([3, 1], attribute.Values.Select(v => v.Id));
    }

    [Fact]
    public async Task Ac_4_7_an_inherited_attribute_carries_the_ancestor_links_settings()
    {
        // The store hands over the parent's link (CategoryId 1) as the leaf's effective attribute (D-12).
        var parentLink = new CategoryAttribute(1, 100, 0, true);
        var color = new AttributeDefinition(100, new LocalizedName("Màu", "Color"), AttributeKind.List, true, 5, true);
        var data = new CategoryDetailData(Leaf, [Root], [new AttributeData(parentLink, color, [new AttributeValue(2, 100, 0, new LocalizedName("Đỏ", "Red"))])]);

        var detail = (await Get(data)).Value!;

        Assert.Equal(10, detail.Id);
        var attribute = Assert.Single(detail.Attributes);
        Assert.Equal((100, true, "multiple", 5, true), (attribute.Id, attribute.Required, attribute.Selection, attribute.MaxValues, attribute.AllowsCustomValue));
    }

    [Fact]
    public async Task A_blank_english_name_falls_back_to_the_vietnamese_one_in_the_detail()
    {
        var blank = new Category(10, 1, 0, new LocalizedName("Lá", string.Empty), true, null);

        var detail = (await Get(new CategoryDetailData(blank, [Root], []))).Value!;

        Assert.Equal("Lá", detail.Name.En);
    }

    private sealed class FakeStore(CategoryDetailData? data) : ICategoryReadStore
    {
        public Task<IReadOnlyList<Category>?> ListChildrenAsync(int? parentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Category>?>(null);

        public Task<CategoryDetailData?> GetDetailAsync(int id, CancellationToken cancellationToken) => Task.FromResult(data);

        public Task<IReadOnlyList<AttributeData>> GetEffectiveAttributesAsync(int categoryId, CancellationToken cancellationToken) =>
            Task.FromResult(data?.Attributes ?? []);
    }
}
