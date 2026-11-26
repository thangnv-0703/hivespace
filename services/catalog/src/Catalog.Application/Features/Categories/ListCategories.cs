using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Catalog.Application.Features.Categories;

public sealed record LocalizedText(string Vi, string En)
{
    /// <summary>FR-LOC-008: a blank English name falls back to the Vietnamese one. The loader never stores a blank (BR-014-4).</summary>
    public static LocalizedText From(LocalizedName name) =>
        new(name.Vi, string.IsNullOrWhiteSpace(name.En) ? name.Vi : name.En);
}

public sealed record CategorySummaryDto(int Id, int? ParentId, LocalizedText Name, bool IsLeaf, int Position, string? ImageUrl)
{
    public static CategorySummaryDto From(Category c) => new(c.Id, c.ParentId, LocalizedText.From(c.Name), c.IsLeaf, c.Position, c.ImageUrl);
}

public sealed record CategoryListDto(IReadOnlyList<CategorySummaryDto> Items, string? AvailabilityProbe);

/// <summary>
/// Lists the roots, or the children of one category (one level per request). Skeleton: also calls Inventory once to prove
/// the gRPC path; a failed call is not fatal.
/// </summary>
public sealed class ListCategoriesHandler(ICategoryReadStore store, IInventoryClient inventory)
{
    public async Task<Result<CategoryListDto>> HandleAsync(int? parentId, CancellationToken cancellationToken)
    {
        var categories = await store.ListChildrenAsync(parentId, cancellationToken).ConfigureAwait(false);
        if (categories is null)
        {
            return Result<CategoryListDto>.Fail(ErrorCodes.NotFound);
        }

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

        return Result<CategoryListDto>.Ok(new CategoryListDto([.. categories.Select(CategorySummaryDto.From)], probe));
    }
}
