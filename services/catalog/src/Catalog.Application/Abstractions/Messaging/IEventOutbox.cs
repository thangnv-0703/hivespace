namespace Catalog.Application.Abstractions;

/// <summary>Writes an event to the outbox in the caller's unit of work (ADR-0002).</summary>
public interface IEventOutbox
{
    Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken);

    /// <summary>Adds the outbox row without saving, so it commits with the caller's next save (a category load, ADR-0002).</summary>
    Guid Stage<T>(string eventType, int version, string correlationId, T data);
}
