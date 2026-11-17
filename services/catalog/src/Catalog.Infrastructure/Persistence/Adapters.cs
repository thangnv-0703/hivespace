using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

internal sealed class CategoryReadStore(CatalogDbContext db) : ICategoryReadStore
{
    public async Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Slug).ToListAsync(cancellationToken).ConfigureAwait(false);
}

internal sealed class EfEventOutbox(CatalogDbContext db) : IEventOutbox
{
    public async Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken)
    {
        var envelope = EventEnvelope.Create(eventType, version, "catalog", correlationId, data);
        db.Set<OutboxMessage>().Add(OutboxMessage.From(envelope));
        // One local transaction: the state change (none for Ping) and the outbox row commit together (ADR-0002).
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return envelope.EventId;
    }
}
