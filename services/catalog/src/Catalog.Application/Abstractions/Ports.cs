using Catalog.Domain.Categories;

namespace Catalog.Application.Abstractions;

/// <summary>Read access to categories. Implemented in Infrastructure (EF Core).</summary>
public interface ICategoryReadStore
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Port to the Inventory service (gRPC, architecture §5.3).</summary>
public interface IInventoryClient
{
    /// <summary>Skeleton probe: proves the Catalog → Inventory gRPC call. Replaced by real availability in FEAT-015.</summary>
    Task<string> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>Writes an event to the outbox in the caller's unit of work (ADR-0002).</summary>
public interface IEventOutbox
{
    Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken);
}
