namespace Catalog.Domain.Categories;

/// <summary>The links a category uses and the category they come from (<c>null</c> when none applies).</summary>
public sealed record EffectiveLinks<TLink>(int? FromCategoryId, IReadOnlyList<TLink> Links);

/// <summary>
/// A leaf's effective attributes (FR-CAT-023, BR-014-10): its own links when it has any; otherwise those of its nearest
/// ancestor that has links; otherwise none. Worked out when read and when checked, never copied (D-12).
/// </summary>
public static class EffectiveAttributes
{
    public static EffectiveLinks<TLink> For<TLink>(
        int categoryId, IReadOnlyDictionary<int, int?> parentOf, IReadOnlyDictionary<int, IReadOnlyList<TLink>> linksByCategory)
    {
        for (int? id = categoryId; id is { } current; id = parentOf.GetValueOrDefault(current))
        {
            if (linksByCategory.TryGetValue(current, out var links) && links.Count > 0)
            {
                return new EffectiveLinks<TLink>(current, links);
            }
        }

        return new EffectiveLinks<TLink>(null, []);
    }
}
