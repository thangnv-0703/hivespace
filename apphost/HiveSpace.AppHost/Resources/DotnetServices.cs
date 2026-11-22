namespace HiveSpace.AppHost.Resources;

internal sealed record DotnetResources(
    IResourceBuilder<ProjectResource> Catalog,
    IResourceBuilder<ProjectResource> Identity,
    IdentitySecrets IdentitySecrets,
    IReadOnlyList<IResourceBuilder<ProjectResource>> Others);

internal static class DotnetServicesExtensions
{
    /// <summary>The browser-facing storefront origin on the gateway. Identity's issuer and the storefront client's URIs are built from it (FEAT-001 D-7).</summary>
    public const string PublicOrigin = "http://localhost:5080";

    /// <summary>The seller centre's own host on the same gateway port; browsers resolve *.localhost to loopback (ADR-0027).</summary>
    public const string SellerOrigin = "http://seller.localhost:5080";

    /// <summary>The per-client settings both Identity and the gateway read (Identity:Clients:*, ADR-0027).</summary>
    public static IResourceBuilder<T> WithOidcClients<T>(this IResourceBuilder<T> resource, IdentitySecrets secrets)
        where T : IResourceWithEnvironment =>
        resource
            .WithEnvironment("Identity__Clients__Storefront__BaseUrl", PublicOrigin)
            .WithEnvironment("Identity__Clients__Storefront__ClientSecret", secrets.StorefrontClientSecret)
            .WithEnvironment("Identity__Clients__Seller__BaseUrl", SellerOrigin)
            .WithEnvironment("Identity__Clients__Seller__ClientSecret", secrets.SellerClientSecret);

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
            .WithEnvironment("Identity__SigningKeyPem", identitySecrets.SigningKeyPem)
            .WithEnvironment("Identity__EncryptionKeyPem", identitySecrets.EncryptionKeyPem)
            .WithOidcClients(identitySecrets)
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
