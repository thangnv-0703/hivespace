using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

/// <summary>Deletes outbox rows sent more than 24 hours ago, because they hold the verification token (R-6, AC-8).</summary>
public sealed partial class OutboxCleanup(IServiceScopeFactory scopeFactory, ILogger<OutboxCleanup> logger) : BackgroundService
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        do
        {
            try
            {
                await RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken).ConfigureAwait(false);
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
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var cutoff = now - Retention;
        return await db.Set<OutboxMessage>()
            .Where(m => m.SentAt != null && m.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox cleanup pass failed; will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
