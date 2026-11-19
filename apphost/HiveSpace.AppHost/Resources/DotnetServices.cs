namespace HiveSpace.AppHost.Resources;

internal sealed record DotnetResources(
    IResourceBuilder<ProjectResource> Catalog,
    IResourceBuilder<ProjectResource> Identity,
    IdentitySecrets IdentitySecrets,
    IReadOnlyList<IResourceBuilder<ProjectResource>> Others);

internal static class DotnetServicesExtensions
{
    /// <summary>The browser-facing origin: the gateway. Identity's issuer and redirect URIs are built from it (FEAT-001 D-7).</summary>
    public const string PublicOrigin = "http://localhost:5080";

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
        var identitySecrets = DevSecrets.LoadIdentitySecrets(builder.AppHostDirectory);
        var identity = builder.AddProject<Projects.Identity_Api>("identity")
            .WithHttpEndpoint()
            .WithEnvironment("Identity__Issuer", PublicOrigin)
            .WithEnvironment("Identity__PublicBaseUrl", PublicOrigin)
            .WithEnvironment("Identity__SigningKeyPem", identitySecrets.SigningKeyPem)
            .WithEnvironment("Identity__EncryptionKeyPem", identitySecrets.EncryptionKeyPem)
            .WithEnvironment("Identity__Gateway__ClientSecret", identitySecrets.GatewayClientSecret)
            .WithReference(identityDb)
            .WithReference(infra.Messaging)
            .WaitFor(identityDb)
            .WaitFor(infra.Messaging);

        // Stubs generated from templates/dotnet-service; each grows its database and references in its slice.
        // They have no launchSettings.json, so they need an explicit endpoint: without one Aspire assigns none
        // and every stub falls back to Kestrel's default port 5000 and collides with the others.
        var others = new List<IResourceBuilder<ProjectResource>>
        {
            builder.AddProject<Projects.Order_Api>("order").WithHttpEndpoint(),
            builder.AddProject<Projects.Payment_Api>("payment").WithHttpEndpoint(),
            builder.AddProject<Projects.Media_Api>("media").WithHttpEndpoint().WithReference(infra.Blobs),
        };

        return new DotnetResources(catalog, identity, identitySecrets, others);
    }
}
