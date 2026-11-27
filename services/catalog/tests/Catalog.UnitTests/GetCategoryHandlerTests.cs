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
        Assert.Equal(("list", "multiple", 3, true), (attribute.Kind, attribute.Selection, attribute.MaxValues, attribute.Required));
        Assert.Equal([2, 1], attribute.Values.Select(v => v.Id));
        Assert.Equal(("Samsung", "Samsung"), (attribute.Values[1].Name.Vi, attribute.Values[1].Name.En));
    }

    [Fact]
    public async Task A_text_attribute_has_no_selection_maximum_or_values()
    {
        var link = new CategoryAttribute(10, 101, 0, false);
        var data = new CategoryDetailData(Leaf, [Root], [new AttributeData(link, new AttributeDefinition(101, new LocalizedName("Chất liệu", "Material"), AttributeKind.Text, false, 1, false), [])]);

        var attribute = Assert.Single((await Get(data)).Value!.Attributes);

        Assert.Equal(("text", null, null), (attribute.Kind, attribute.Selection, attribute.MaxValues));
        Assert.Empty(attribute.Values);
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
    }
}
