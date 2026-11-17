using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;
using Xunit;

namespace Catalog.UnitTests;

public sealed class ListCategoriesHandlerTests
{
    private static readonly Category Fashion = new(Guid.CreateVersion7(), "thoi-trang", "Thời trang", "Fashion");

    [Fact]
    public async Task Returns_categories_with_both_names_and_the_inventory_probe()
    {
        var handler = new ListCategoriesHandler(new FakeStore(Fashion), new FakeInventory("ok"));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Items);
        Assert.Equal("Thời trang", item.Name.Vi);
        Assert.Equal("Fashion", item.Name.En);
        Assert.Equal("ok", result.AvailabilityProbe);
    }

    [Fact]
    public async Task Still_returns_categories_when_inventory_is_down()
    {
        var handler = new ListCategoriesHandler(new FakeStore(Fashion), new FakeInventory(null));

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.Single(result.Items);
        Assert.Null(result.AvailabilityProbe);
    }

    private sealed class FakeStore(params Category[] categories) : ICategoryReadStore
    {
        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Category>>(categories);
    }

    private sealed class FakeInventory(string? probe) : IInventoryClient
    {
        public Task<string> ProbeAsync(CancellationToken cancellationToken) =>
            probe is null ? throw new InvalidOperationException("inventory down") : Task.FromResult(probe);
    }
}
