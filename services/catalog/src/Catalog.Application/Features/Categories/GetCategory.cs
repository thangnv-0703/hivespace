using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Catalog.Application.Features.Categories;

public sealed record CategoryRefDto(int Id, LocalizedText Name);

public sealed record AttributeValueDto(int Id, LocalizedText Name);

public sealed record CategoryAttributeDto(
    int Id, LocalizedText Name, bool Required, string Kind, string? Selection, int? MaxValues, IReadOnlyList<AttributeValueDto> Values);

/// <summary>A category summary plus its path and attributes (<c>CategoryDetail</c> in catalog.yaml).</summary>
public sealed record CategoryDetailDto(
    int Id, int? ParentId, LocalizedText Name, bool IsLeaf, int Position, string? ImageUrl,
    IReadOnlyList<CategoryRefDto> Path, IReadOnlyList<CategoryAttributeDto> Attributes);

/// <summary>One category with its path and, for a leaf, its attributes and values (FR-CAT-020, UC-005).</summary>
public sealed class GetCategoryHandler(ICategoryReadStore store)
{
    public async Task<Result<CategoryDetailDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var detail = await store.GetDetailAsync(id, cancellationToken).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<CategoryDetailDto>.Fail(ErrorCodes.NotFound);
        }

        var c = detail.Category;
        return Result<CategoryDetailDto>.Ok(new CategoryDetailDto(
            c.Id, c.ParentId, LocalizedText.From(c.Name), c.IsLeaf, c.Position, c.ImageUrl,
            [.. detail.Path.Select(p => new CategoryRefDto(p.Id, LocalizedText.From(p.Name)))],
            [.. detail.Attributes.Select(Map)]));
    }

    private static CategoryAttributeDto Map(AttributeData a)
    {
        var isList = a.Definition.Kind == AttributeKind.List;
        return new CategoryAttributeDto(
            a.Definition.Id,
            LocalizedText.From(a.Definition.Name),
            a.Link.IsRequired,
            isList ? "list" : "text",
            isList ? (a.Definition.AllowsMultiple ? "multiple" : "single") : null,
            isList ? a.Definition.MaxValues : null,
            [.. a.Values.Select(v => new AttributeValueDto(v.Id, LocalizedText.From(v.Name)))]);
    }
}
