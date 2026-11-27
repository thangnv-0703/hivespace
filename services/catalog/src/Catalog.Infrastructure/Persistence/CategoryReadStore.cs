using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

internal sealed class CategoryReadStore(CatalogDbContext db) : ICategoryReadStore
{
    public async Task<IReadOnlyList<Category>?> ListChildrenAsync(int? parentId, CancellationToken cancellationToken)
    {
        if (parentId is { } p && !await db.Categories.AnyAsync(c => c.Id == p, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await db.Categories.AsNoTracking().Where(c => c.ParentId == parentId).OrderBy(c => c.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<CategoryDetailData?> GetDetailAsync(int id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
        if (category is null)
        {
            return null;
        }

        var path = await PathAsync(category, cancellationToken).ConfigureAwait(false);

        // Products go only in leaves, so only a leaf has attributes (FR-CAT-023, D-12).
        IReadOnlyList<AttributeData> attributes = category.IsLeaf ? await EffectiveAsync(category, path, cancellationToken).ConfigureAwait(false) : [];
        return new CategoryDetailData(category, path, attributes);
    }

    public async Task<IReadOnlyList<AttributeData>> GetEffectiveAttributesAsync(int categoryId, CancellationToken cancellationToken)
    {
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken).ConfigureAwait(false);
        if (category is not { IsLeaf: true })
        {
            return [];
        }

        return await EffectiveAsync(category, await PathAsync(category, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ancestors, root first. The tree is shallow (depth 8 in the source data), and FromSql with a recursive query does not
    /// map complex-property columns, so the parents are read one at a time.
    /// </summary>
    private async Task<List<Category>> PathAsync(Category category, CancellationToken cancellationToken)
    {
        var path = new List<Category>();
        for (var parentId = category.ParentId; parentId is { } p;)
        {
            var parent = await db.Categories.AsNoTracking().SingleAsync(c => c.Id == p, cancellationToken).ConfigureAwait(false);
            path.Insert(0, parent);
            parentId = parent.ParentId;
        }

        return path;
    }

    /// <summary>The links of the leaf and its ancestors in one query, picked by <see cref="EffectiveAttributes"/>.</summary>
    private async Task<IReadOnlyList<AttributeData>> EffectiveAsync(Category leaf, IReadOnlyList<Category> path, CancellationToken cancellationToken)
    {
        var lineage = path.Append(leaf).ToList();
        var categoryIds = lineage.Select(c => c.Id).ToList();
        var links = await db.CategoryAttributes.AsNoTracking().Where(l => categoryIds.Contains(l.CategoryId)).OrderBy(l => l.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, IReadOnlyList<CategoryAttribute>> linksByCategory =
            links.GroupBy(l => l.CategoryId).ToDictionary(g => g.Key, g => (IReadOnlyList<CategoryAttribute>)[.. g]);
        var effective = EffectiveAttributes.For(leaf.Id, lineage.ToDictionary(c => c.Id, c => c.ParentId), linksByCategory).Links;
        if (effective.Count == 0)
        {
            return [];
        }

        var attributeIds = effective.Select(l => l.AttributeId).ToList();
        var definitions = await db.Attributes.AsNoTracking().Where(a => attributeIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken).ConfigureAwait(false);
        var values = (await db.AttributeValues.AsNoTracking().Where(v => attributeIds.Contains(v.AttributeId)).OrderBy(v => v.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(v => v.AttributeId);
        return [.. effective.Select(l => new AttributeData(l, definitions[l.AttributeId], [.. values[l.AttributeId]]))];
    }
}
