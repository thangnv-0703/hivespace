namespace HiveSpace.AppHost.Resources;

internal sealed record DotnetResources(IResourceBuilder<ProjectResource> Catalog, IReadOnlyList<IResourceBuilder<ProjectResource>> Others);

internal static class DotnetServicesExtensions
{
    /// <summary>.NET services (ADR-0001, ADR-0020). Each gets its own database; none reads another's (ADR-0018).</summary>
    public static DotnetResources AddDotnetServices(this IDistributedApplicationBuilder builder, InfrastructureResources infra, GoResources go)
    {
        var catalogDb = infra.Postgres.AddDatabase("catalogdb");
        var catalog = builder.AddProject<Projects.Catalog_Api>("catalog")
            .WithReference(catalogDb)
            .WithReference(infra.Messaging)
            .WithReference(go.Inventory.GetEndpoint("grpc"))
            .WaitFor(catalogDb)
            .WaitFor(infra.Messaging);

        var identityDb = infra.Postgres.AddDatabase("identitydb");
        var identity = builder.AddProject<Projects.Identity_Api>("identity")
            .WithHttpEndpoint()
            .WithReference(identityDb)
            .WithReference(infra.Messaging)
            .WaitFor(identityDb)
            .WaitFor(infra.Messaging);

        // Stubs generated from templates/dotnet-service; each grows its database and references in its slice.
        // They have no launchSettings.json, so they need an explicit endpoint: without one Aspire assigns none
        // and every stub falls back to Kestrel's default port 5000 and collides with the others.
        var others = new List<IResourceBuilder<ProjectResource>>
        {
            identity,
            builder.AddProject<Projects.Order_Api>("order").WithHttpEndpoint(),
            builder.AddProject<Projects.Payment_Api>("payment").WithHttpEndpoint(),
            builder.AddProject<Projects.Media_Api>("media").WithHttpEndpoint().WithReference(infra.Blobs),
        };

        return new DotnetResources(catalog, others);
    }
}
