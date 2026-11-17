namespace HiveSpace.AppHost.Resources;

internal sealed record GoResources(
    IResourceBuilder<Aspire.Hosting.Go.GoAppResource> Inventory,
    IResourceBuilder<Aspire.Hosting.Go.GoAppResource> Cart);

internal static class GoServicesExtensions
{
    /// <summary>Go services (ADR-0007, ADR-0017). HTTP on PORT; gRPC servers on GRPC_PORT.</summary>
    public static GoResources AddGoServices(this IDistributedApplicationBuilder builder, InfrastructureResources infra)
    {
        var inventoryDb = infra.Postgres.AddDatabase("inventorydb");
        var inventory = builder.AddGoApp("inventory", "../../services/inventory", "./cmd/inventory")
            .WithHttpEndpoint(env: "PORT")
            .WithHttpEndpoint(name: "grpc", env: "GRPC_PORT")
            .WithEnvironment("DATABASE_URL", inventoryDb.Resource.UriExpression)
            .WithReference(infra.Messaging)
            .WithEnvironment("EVENTS_TOPIC", InfrastructureExtensions.EventsTopic)
            .WithEnvironment("EVENTS_SUBSCRIPTION", "inventory")
            .WithOtlpExporter()
            .WithHttpHealthCheck("/health")
            .WaitFor(inventoryDb)
            .WaitFor(infra.Messaging);

        // Stub from templates/go-service; its gRPC server arrives with FEAT-021.
        var cart = builder.AddGoApp("cart", "../../services/cart", "./cmd/cart")
            .WithHttpEndpoint(env: "PORT")
            .WithReference(infra.CartDb)
            .WithOtlpExporter()
            .WithHttpHealthCheck("/health")
            .WaitFor(infra.CartDb);

        return new GoResources(inventory, cart);
    }
}
