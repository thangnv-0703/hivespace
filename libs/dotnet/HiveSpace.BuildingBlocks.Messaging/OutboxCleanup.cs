using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiveSpace.BuildingBlocks.Messaging;

/// <summary>How long a sent outbox row is kept before <see cref="OutboxCleanup{TContext}"/> deletes it.</summary>
public sealed record OutboxCleanupOptions(TimeSpan Retention)
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromHours(24);
}

/// <summary>Deletes outbox rows sent longer ago than <see cref="OutboxCleanupOptions.Retention"/>. Opt-in per service.</summary>
public sealed partial class OutboxCleanup<TContext>(
    IServiceScopeFactory scopeFactory,
    OutboxCleanupOptions options,
    TimeProvider timeProvider,
    ILogger<OutboxCleanup<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10), timeProvider);
        do
        {
            try
            {
                await RunOnceAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var cutoff = now - options.Retention;
        return await db.Set<OutboxMessage>()
            .Where(m => m.SentAt != null && m.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox cleanup pass failed; will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
