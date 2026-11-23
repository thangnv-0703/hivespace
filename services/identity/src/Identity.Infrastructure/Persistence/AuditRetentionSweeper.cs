using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Deletes audit rows older than 90 days, on start and every 6 hours (NFR-SEC-009, architecture §8 "purged after 90 days"). The shape
/// is the one of <c>OutboxCleanup</c> in the messaging library.
/// </summary>
public sealed partial class AuditRetentionSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuditRetentionSweeper> logger) : BackgroundService
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
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

    /// <summary>One pass; public for tests. Returns how many rows it deleted.</summary>
    public async Task<int> RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var cutoff = now - Retention;
        return await db.AuditEntries
            .Where(a => a.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit retention pass failed; will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
