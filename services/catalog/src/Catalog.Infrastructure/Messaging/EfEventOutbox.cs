using Catalog.Application.Abstractions;
using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Messaging;

namespace Catalog.Infrastructure.Messaging;

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
