using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Deletes sign-in code challenges one day after they expire, on start and every hour (FEAT-006 D-3, NFR-LEG-001). The 60-second
/// rule needs only the last minute; a day leaves room for investigation. Same shape as <see cref="AuditRetentionSweeper"/>.
/// </summary>
public sealed partial class SignInChallengeSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SignInChallengeSweeper> logger) : BackgroundService
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(1);

    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
        return await db.SignInCodeChallenges
            .Where(c => c.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in challenge purge failed; will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
