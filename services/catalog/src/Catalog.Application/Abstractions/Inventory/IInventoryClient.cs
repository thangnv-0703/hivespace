namespace Catalog.Application.Abstractions;

/// <summary>Port to the Inventory service (gRPC, architecture §5.3).</summary>
public interface IInventoryClient
{
    /// <summary>Skeleton probe: proves the Catalog → Inventory gRPC call. Replaced by real availability in FEAT-015.</summary>
    Task<string> ProbeAsync(CancellationToken cancellationToken);
}
