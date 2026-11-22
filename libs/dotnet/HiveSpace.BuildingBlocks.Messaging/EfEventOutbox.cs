using HiveSpace.BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HiveSpace.BuildingBlocks.Messaging;

/// <summary>
/// Adds the outbox row to the caller's <typeparamref name="TContext"/>, so it commits with the state change (ADR-0002).
/// The producer is <see cref="MessagingOptions.Producer"/>; <see cref="MessagingExtensions.AddEventBus{TContext}"/> registers it.
/// </summary>
public sealed class EfEventOutbox<TContext>(TContext db, MessagingOptions options) : IEventOutbox
    where TContext : DbContext
{
    public void Enqueue<T>(string eventType, int version, string correlationId, T data) =>
        db.Set<OutboxMessage>().Add(OutboxMessage.From(EventEnvelope.Create(eventType, version, options.Producer, correlationId, data)));
}
