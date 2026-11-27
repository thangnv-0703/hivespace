using Catalog.Domain.Categories;

namespace Catalog.Application.Abstractions;

/// <summary>An attribute of a leaf: its link, its definition with the list settings, and its values ordered by position.</summary>
public sealed record AttributeData(CategoryAttribute Link, AttributeDefinition Definition, IReadOnlyList<AttributeValue> Values);

/// <summary>A category with its path from the root (parent last) and, for a leaf, its effective attributes in file order.</summary>
public sealed record CategoryDetailData(Category Category, IReadOnlyList<Category> Path, IReadOnlyList<AttributeData> Attributes);

/// <summary>Read access to the category tree. Implemented in Infrastructure (EF Core).</summary>
public interface ICategoryReadStore
{
    /// <summary>The children of <paramref name="parentId"/> (the roots when null) ordered by position; <c>null</c> when the parent is not a category.</summary>
    Task<IReadOnlyList<Category>?> ListChildrenAsync(int? parentId, CancellationToken cancellationToken);

    /// <summary><c>null</c> when the category does not exist. A leaf's attributes are its effective ones (FR-CAT-023).</summary>
    Task<CategoryDetailData?> GetDetailAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// A leaf's effective attributes (D-12): its own links, or else those of its nearest ancestor that has links, in that
    /// category's order. Empty for a category that is not a leaf or does not exist. FEAT-012 validates products with it.
    /// </summary>
    Task<IReadOnlyList<AttributeData>> GetEffectiveAttributesAsync(int categoryId, CancellationToken cancellationToken);
}
