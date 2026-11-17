using Catalog.Application.Abstractions;

namespace Catalog.Application.Features.Ping;

public sealed record PingEvent(string Message);

/// <summary>Skeleton only: writes a Ping event to the outbox to prove outbox → broker → inbox. Removed after FEAT-012.</summary>
public sealed class PublishPingHandler(IEventOutbox outbox)
{
    public const string EventType = "Ping";

    public Task<Guid> HandleAsync(string correlationId, CancellationToken cancellationToken) =>
        outbox.WriteAsync(EventType, 1, correlationId, new PingEvent("hello from catalog"), cancellationToken);
}
