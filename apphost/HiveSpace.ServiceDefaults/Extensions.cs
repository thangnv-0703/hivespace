using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// The hosting contract every .NET process in HiveSpace follows (repo-structure §5, dev-standards §11):
// OpenTelemetry, JSON logs with a correlation ID, /health and /ready, service discovery and resilience.
// Go (libs/go/otelx) and Python (hivespace_common.otel) implement the same contract by hand.
public static class Extensions
{
    public const string CorrelationHeader = "X-Correlation-ID";
    private const string HealthEndpointPath = "/health";
    private const string ReadyEndpointPath = "/ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureJsonLogging();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>Structured JSON console logs with scopes, so the correlation ID appears on every line (NFR-OBS-002).</summary>
    public static TBuilder ConfigureJsonLogging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        });
        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Azure.Messaging.ServiceBus.*")
                    .AddAspNetCoreInstrumentation(tracing =>
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(ReadyEndpointPath)
                    )
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Liveness only; dependency checks are tagged "ready" by each service.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps /health (liveness) and /ready (all checks, including dependencies), and the correlation-ID middleware.
    /// Both endpoints return only a status word, so they are safe outside Development.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            }

            context.Response.Headers[CorrelationHeader] = correlationId;
            Activity.Current?.SetTag("correlation.id", correlationId);
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlation");
            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                await next(context);
            }
        });

        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
        });
        app.MapHealthChecks(ReadyEndpointPath);

        return app;
    }

    /// <summary>The correlation ID for the current request, for events and logs.</summary>
    public static string GetCorrelationId(this HttpContext context) =>
        context.Response.Headers[CorrelationHeader].FirstOrDefault()
        ?? Activity.Current?.TraceId.ToString()
        ?? Guid.NewGuid().ToString("N");
}
