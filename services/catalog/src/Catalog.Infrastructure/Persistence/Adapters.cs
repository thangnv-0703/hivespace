using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Messaging;
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
            var valueIds = links.SelectMany(l => l.ValueIds).Distinct().ToList();
            var values = await db.AttributeValues.AsNoTracking().Where(v => valueIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken).ConfigureAwait(false);
            attributes = [.. links.Select(l => new AttributeData(l, definitions[l.AttributeId], [.. l.ValueIds.Select(v => values[v])]))];
        }

        return new CategoryDetailData(category, path, attributes);
    }
}

internal sealed class EfEventOutbox(CatalogDbContext db) : IEventOutbox
{
    public Guid Stage<T>(string eventType, int version, string correlationId, T data)
    {
        var envelope = EventEnvelope.Create(eventType, version, "catalog", correlationId, data);
        db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
        return envelope.EventId;
    }

    public async Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken)
    {
        var envelope = EventEnvelope.Create(eventType, version, "catalog", correlationId, data);
        db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
        // One local transaction: the state change (none for Ping) and the outbox row commit together (ADR-0002).
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return envelope.EventId;
    }
}
