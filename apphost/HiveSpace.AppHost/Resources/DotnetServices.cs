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

    /// <summary>The fake IdP's fixed local port; E2E steps reach its <c>/__fake/next</c> control endpoint there (FEAT-004 T9).</summary>
    public const int FakeIdpPort = 5090;

    /// <summary>
    /// Google sign-in (FEAT-004 §5.6, D-7): a real test client when <c>.local/dev-secrets/google.json</c> exists, otherwise the fake
    /// IdP from <c>tools/fake-idp</c> on <see cref="FakeIdpPort"/>. Identity does not wait for it: Google being down must never
    /// block password sign-in (FR-AUTH-023).
    /// </summary>
    private static void WithGoogle(this IResourceBuilder<ProjectResource> identity, IDistributedApplicationBuilder builder, GoogleClient google)
    {
        var authority = google.Authority;
        if (!google.Real)
        {
            authority = $"http://localhost:{FakeIdpPort}";
            builder.AddProject<Projects.FakeIdp>("fake-idp", launchProfileName: null)
                .WithHttpEndpoint(port: FakeIdpPort)
                .WithHttpHealthCheck("/health")
                .WithEnvironment("FakeIdp__ClientId", google.ClientId)
                .WithEnvironment("FakeIdp__ClientSecret", google.ClientSecret)
                .WithEnvironment("FakeIdp__RedirectUris__0", PublicOrigin + "/connect/external/google/callback");
        }

        identity
            .WithEnvironment("Identity__Google__ClientId", google.ClientId)
            .WithEnvironment("Identity__Google__ClientSecret", google.ClientSecret)
            .WithEnvironment("Identity__Google__Authority", authority);
    }

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

        // The operator's category load (FEAT-014, D-3): started by hand from the dashboard, report in its console log.
        // It waits for catalog so migrations ran, takes no messaging reference (the running catalog relays the event)
        // and stays out of the deployed profile.
        builder.AddProject<Projects.Catalog_Api>("catalog-load-categories", launchProfileName: null)
            .WithArgs("load-categories", "--source", Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "data", "category-tree")))
            .WithReference(catalogDb)
            .WaitFor(catalog)
            .WithExplicitStart();

        var identityDb = infra.Postgres.AddDatabase("identitydb");
        var identitySecrets = DevSecrets.LoadIdentitySecrets(builder.AppHostDirectory);
        var identity = builder.AddProject<Projects.Identity_Api>("identity")
            .WithHttpEndpoint()
            .WithEnvironment("Identity__Issuer", PublicOrigin)
            .WithEnvironment("Identity__SigningKeyPem", identitySecrets.SigningKeyPem)
            .WithEnvironment("Identity__EncryptionKeyPem", identitySecrets.EncryptionKeyPem)
            .WithEnvironment("Identity__BankEncryption__ActiveKeyId", "v1")
            .WithEnvironment("Identity__BankEncryption__Keys__v1", identitySecrets.BankEncryptionKeyV1)
            .WithOidcClients(identitySecrets)
            .WithReference(identityDb)
            .WithReference(infra.Messaging)
            .WaitFor(identityDb)
            .WaitFor(infra.Messaging);
        identity.WithGoogle(builder, DevSecrets.LoadGoogleClient(builder.AppHostDirectory));

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
