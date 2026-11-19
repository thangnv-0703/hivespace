using Aspire.Hosting.Azure;

namespace HiveSpace.AppHost.Resources;

/// <summary>Backing services. Locally these are containers and Azure emulators, never real cloud resources (ADR-0015).</summary>
internal sealed record InfrastructureResources(
    IResourceBuilder<PostgresServerResource> Postgres,
    IResourceBuilder<AzureServiceBusResource> Messaging,
    IResourceBuilder<MongoDBDatabaseResource> CartDb,
    IResourceBuilder<AzureBlobStorageResource> Blobs,
    IResourceBuilder<MailPitContainerResource> MailPit);

internal static class InfrastructureExtensions
{
    /// <summary>One topic for every domain event; one subscription per consuming service (ADR-0002).</summary>
    public const string EventsTopic = "hivespace-events";

    /// <summary>Mailpit web UI and API on the host; the Playwright suite reads the inbox here.</summary>
    public const int MailpitHttpPort = 8025;

    public static InfrastructureResources AddInfrastructure(this IDistributedApplicationBuilder builder)
    {
        // pgvector on Postgres 17, matching the Neon major version planned for the demo (ADR-0003, ADR-0005).
        var postgres = builder.AddPostgres("postgres")
            .WithImage("pgvector/pgvector")
            .WithImageTag("pg17")
            .WithDataVolume("hivespace-postgres")
            .WithLifetime(ContainerLifetime.Persistent);

        var messaging = builder.AddAzureServiceBus("messaging").RunAsEmulator();
        var topic = messaging.AddServiceBusTopic(EventsTopic);
        topic.AddServiceBusSubscription("inventory-events", "inventory");
        topic.AddServiceBusSubscription("notification-events", "notification");

        var cartDb = builder.AddMongoDB("mongo")
            .WithDataVolume("hivespace-mongo")
            .WithLifetime(ContainerLifetime.Persistent)
            .AddDatabase("cartdb");

        var blobs = builder.AddAzureStorage("storage")
            .RunAsEmulator(azurite => azurite.WithDataVolume("hivespace-azurite"))
            .AddBlobs("blobs");

        // Fixed HTTP port so the Playwright suite can read the inbox without asking the orchestrator (tests/e2e).
        var mailpit = builder.AddMailPit("mailpit", httpPort: MailpitHttpPort);

        return new InfrastructureResources(postgres, messaging, cartDb, blobs, mailpit);
    }
}
