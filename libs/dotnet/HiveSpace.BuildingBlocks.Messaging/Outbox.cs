using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiveSpace.BuildingBlocks.Messaging;

/// <summary>A row in a service's <c>outbox</c> table; written in the same transaction as the state change (ADR-0002).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public required string EventType { get; set; }

    public required string Envelope { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public static OutboxMessage From(EventEnvelope envelope) => new()
    {
        Id = envelope.EventId,
        EventType = envelope.EventType,
        Envelope = envelope.ToJson(),
        OccurredAt = envelope.OccurredAt,
    };
}

/// <summary>A row in a service's <c>inbox</c> table; its key makes event handling idempotent (NFR-REL-005).</summary>
public sealed class InboxMessage
{
    public Guid EventId { get; set; }

    public required string Consumer { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}

/// <summary>Publishes envelopes to the broker. Implementations are chosen by configuration (ADR-0002).</summary>
public interface IEventBus
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>Azure Service Bus implementation: one topic, event type and trace context as application properties.</summary>
public sealed class ServiceBusEventBus(ServiceBusClient client, MessagingOptions options) : IEventBus, IAsyncDisposable
{
    private readonly ServiceBusSender _sender = client.CreateSender(options.Topic);

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(envelope.ToJson())
        {
            MessageId = envelope.EventId.ToString(),
            ContentType = "application/json",
            CorrelationId = envelope.CorrelationId,
            Subject = envelope.EventType,
        };
        message.ApplicationProperties["eventType"] = envelope.EventType;
        if (envelope.Traceparent is not null)
        {
            message.ApplicationProperties["traceparent"] = envelope.Traceparent;
        }

        await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}

public sealed record MessagingOptions(string Topic, string Producer);

/// <summary>Polls the outbox and publishes unsent rows (ADR-0002). At-least-once: consumers dedupe by event id.</summary>
public sealed partial class OutboxRelay<TContext>(
    IServiceScopeFactory scopeFactory,
    IEventBus bus,
    ILogger<OutboxRelay<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private static readonly ActivitySource Source = new("HiveSpace.Outbox");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await PublishPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRelayFailed(logger, ex);
            }
        }
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in pending)
        {
            var envelope = EventEnvelope.FromJson(message.Envelope);
            using var activity = Source.StartActivity(
                $"publish {envelope.EventType}",
                ActivityKind.Producer,
                envelope.Traceparent);
            await bus.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
            message.SentAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox relay pass failed; will retry")]
    private static partial void LogRelayFailed(ILogger logger, Exception exception);
}

public static class MessagingExtensions
{
    /// <summary>Maps the <c>outbox</c> and <c>inbox</c> tables every service owns (repo-structure §3).</summary>
    public static ModelBuilder AddOutboxAndInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox");
            e.HasKey(m => m.Id);
            e.Property(m => m.EventType).HasMaxLength(200);
            e.Property(m => m.Envelope).HasColumnType("jsonb");
            e.HasIndex(m => m.SentAt);
        });
        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.ToTable("inbox");
            e.HasKey(m => new { m.EventId, m.Consumer });
            e.Property(m => m.Consumer).HasMaxLength(200);
        });
        return modelBuilder;
    }

    /// <summary>Registers the Service Bus event bus and the outbox relay for <typeparamref name="TContext"/>.</summary>
    public static IHostApplicationBuilder AddEventBus<TContext>(
        this IHostApplicationBuilder builder,
        string connectionName,
        MessagingOptions options)
        where TContext : DbContext
    {
        builder.AddAzureServiceBusClient(connectionName);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IEventBus, ServiceBusEventBus>();
        builder.Services.AddHostedService<OutboxRelay<TContext>>();
        builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("HiveSpace.Outbox"));
        return builder;
    }
}
