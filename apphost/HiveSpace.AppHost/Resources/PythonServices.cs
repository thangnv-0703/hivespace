using Aspire.Hosting.Python;

namespace HiveSpace.AppHost.Resources;

internal sealed record PythonResources(IResourceBuilder<UvicornAppResource> SearchAi);

internal static class PythonServicesExtensions
{
    /// <summary>Python services (ADR-0016), FastAPI under uvicorn, environments managed by uv.</summary>
    public static PythonResources AddPythonServices(this IDistributedApplicationBuilder builder, InfrastructureResources infra)
    {
        var notificationDb = infra.Postgres.AddDatabase("notificationdb");
        builder.AddUvicornApp("notification", "../../services/notification", "notification.main:app")
            .WithUv()
            .WithEnvironment("DATABASE_URL", notificationDb.Resource.UriExpression)
            .WithReference(infra.Messaging)
            .WithEnvironment("EVENTS_TOPIC", InfrastructureExtensions.EventsTopic)
            .WithEnvironment("EVENTS_SUBSCRIPTION", "notification")
            .WithEnvironment("PUBLIC_BASE_URL", DotnetServicesExtensions.PublicOrigin)
            .WithReference(infra.MailPit)
            .WithOtlpExporter()
            .WithHttpHealthCheck("/health")
            .WaitFor(notificationDb)
            .WaitFor(infra.Messaging);

        // Stub from templates/python-service; read model and NL search arrive with FEAT-018 and FEAT-019.
        var searchAi = builder.AddUvicornApp("search-ai", "../../services/search-ai", "search_ai.main:app")
            .WithUv()
            .WithOtlpExporter()
            .WithHttpHealthCheck("/health");

        return new PythonResources(searchAi);
    }
}
