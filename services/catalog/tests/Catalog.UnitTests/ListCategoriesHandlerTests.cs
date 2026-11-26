using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Abstractions;
using Xunit;

namespace Catalog.UnitTests;

public sealed class ListCategoriesHandlerTests
{
    private static readonly Category Fashion = new(1, null, 0, new LocalizedName("Thời trang", "Fashion"), true, "https://example.com/a.jpg");

    [Fact]
    public async Task Returns_categories_with_both_names_the_image_and_the_inventory_probe()
    {
        var handler = new ListCategoriesHandler(new FakeStore(Fashion), new FakeInventory("ok"));

        var result = await handler.HandleAsync(null, TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(("Thời trang", "Fashion", "https://example.com/a.jpg"), (item.Name.Vi, item.Name.En, item.ImageUrl));
        Assert.True(item.IsLeaf);
        Assert.Equal("ok", result.Value.AvailabilityProbe);
    }

    [Fact]
    public async Task Still_returns_categories_when_inventory_is_down()
    {
        var handler = new ListCategoriesHandler(new FakeStore(Fashion), new FakeInventory(null));

        var result = await handler.HandleAsync(null, TestContext.Current.CancellationToken);

        Assert.Single(result.Value!.Items);
        Assert.Null(result.Value.AvailabilityProbe);
    }

    [Fact]
    public async Task An_unknown_parent_is_not_found()
    {
        var handler = new ListCategoriesHandler(new FakeStore(), new FakeInventory("ok"));

        var result = await handler.HandleAsync(999, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.NotFound, result.Failure!.Code);
    }

    [Fact]
    public async Task A_blank_english_name_falls_back_to_the_vietnamese_one()
    {
        var blank = new Category(2, null, 0, new LocalizedName("Nhà cửa", "  "), true, null);
        var handler = new ListCategoriesHandler(new FakeStore(blank), new FakeInventory("ok"));

        var result = await handler.HandleAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal("Nhà cửa", result.Value!.Items.Single().Name.En);
    }

    private sealed class FakeStore(params Category[] categories) : ICategoryReadStore
    {
        public Task<IReadOnlyList<Category>?> ListChildrenAsync(int? parentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>?>(parentId is null || categories.Any(c => c.Id == parentId) ? categories : null);

        public Task<CategoryDetailData?> GetDetailAsync(int id, CancellationToken cancellationToken) => Task.FromResult<CategoryDetailData?>(null);
    }

    private sealed class FakeInventory(string? probe) : IInventoryClient
    {
        public Task<string> ProbeAsync(CancellationToken cancellationToken) =>
            probe is null ? throw new InvalidOperationException("inventory down") : Task.FromResult(probe);
    }
}
