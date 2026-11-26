using Catalog.Application.Abstractions;

namespace Catalog.Application.Features.Categories;

public sealed record LocalizedText(string Vi, string En);

public sealed record CategoryDto(int Id, LocalizedText Name);

public sealed record CategoryListDto(IReadOnlyList<CategoryDto> Items, string? AvailabilityProbe);

/// <summary>Lists categories. Skeleton: also calls Inventory once to prove the gRPC path; a failed call is not fatal.</summary>
public sealed class ListCategoriesHandler(ICategoryReadStore store, IInventoryClient inventory)
{
    public async Task<CategoryListDto> HandleAsync(CancellationToken cancellationToken)
    {
        var categories = await store.ListAsync(cancellationToken).ConfigureAwait(false);

        string? probe;
        try
        {
            probe = await inventory.ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Architecture §5.3: if Inventory is down the page still renders, availability shows as unknown.
            probe = null;
        }

        var items = categories
            .Select(c => new CategoryDto(c.Id, new LocalizedText(c.Name.Vi, c.Name.En)))
            .ToList();
        return new CategoryListDto(items, probe);
    }
}
