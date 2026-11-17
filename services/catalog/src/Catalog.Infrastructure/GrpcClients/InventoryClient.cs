using Catalog.Application.Abstractions;
using HiveSpace.Contracts.Inventory.V1;

namespace Catalog.Infrastructure.GrpcClients;

internal sealed class InventoryClient(InventoryService.InventoryServiceClient client) : IInventoryClient
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<string> ProbeAsync(CancellationToken cancellationToken)
    {
        // Every internal call sets a deadline (dev-standards §6.1).
        var response = await client.GetAvailabilityAsync(
            new GetAvailabilityRequest(),
            deadline: DateTime.UtcNow.Add(Deadline),
            cancellationToken: cancellationToken);
        return response.Probe;
    }
}
