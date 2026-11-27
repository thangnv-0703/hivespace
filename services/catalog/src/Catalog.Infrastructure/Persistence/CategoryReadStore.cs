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

        // Ancestors, root first. The tree is shallow (depth 8 in the source data), and FromSql with a recursive query does
        // not map complex-property columns, so the parents are read one at a time.
        var path = new List<Category>();
        for (var parentId = category.ParentId; parentId is { } p;)
        {
            var parent = await db.Categories.AsNoTracking().SingleAsync(c => c.Id == p, cancellationToken).ConfigureAwait(false);
            path.Insert(0, parent);
            parentId = parent.ParentId;
        }

        // Attributes belong to a leaf only (BR-014-10, D-5).
        var attributes = new List<AttributeData>();
        if (category.IsLeaf)
        {
            var links = await db.CategoryAttributes.AsNoTracking().Where(l => l.CategoryId == id).OrderBy(l => l.Position)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var attributeIds = links.Select(l => l.AttributeId).ToList();
            var definitions = await db.Attributes.AsNoTracking().Where(a => attributeIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken).ConfigureAwait(false);
            var values = (await db.AttributeValues.AsNoTracking().Where(v => attributeIds.Contains(v.AttributeId)).OrderBy(v => v.Position)
                .ToListAsync(cancellationToken).ConfigureAwait(false)).ToLookup(v => v.AttributeId);
            attributes = [.. links.Select(l => new AttributeData(l, definitions[l.AttributeId], [.. values[l.AttributeId]]))];
        }

        return new CategoryDetailData(category, path, attributes);
    }
}
