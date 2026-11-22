namespace HiveSpace.BuildingBlocks.Abstractions;

/// <summary>
/// Queues an event in the caller's unit of work; it commits with the service's next save, in the same transaction as the
/// state change (ADR-0002). The producer name and the envelope are filled in by the implementation.
/// </summary>
public interface IEventOutbox
{
    void Enqueue<T>(string eventType, int version, string correlationId, T data);
}
