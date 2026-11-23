using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-005 T5: AC-9, the audit log is purged after 90 days (NFR-SEC-009).</summary>
public sealed class AuditRetentionTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac9_RunOnceAsync_deletes_rows_older_than_90_days_and_keeps_one_89_days_old()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.CreateVersion7();
        await using (var db = postgres.CreateContext())
        {
            db.AuditEntries.AddRange(
                AuditEntry.SignInFailed(userId, AuditMethod.Password, "identity.invalid_credentials", now.AddDays(-91)),
                AuditEntry.SignInFailed(userId, AuditMethod.Password, "identity.invalid_credentials", now.AddDays(-90).AddMinutes(-1)),
                AuditEntry.SignInSucceeded(userId, AuditMethod.Google, now.AddDays(-89)),
                AuditEntry.SignInSucceeded(userId, AuditMethod.Password, now));
            await db.SaveChangesAsync(Ct);
        }

        var services = new ServiceCollection()
            .AddScoped(_ => postgres.CreateContext())
            .BuildServiceProvider();
        var sweeper = new AuditRetentionSweeper(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<AuditRetentionSweeper>.Instance);

        var deleted = await sweeper.RunOnceAsync(now, Ct);

        Assert.Equal(2, deleted);
        await using var read = postgres.CreateContext();
        var left = await read.AuditEntries.AsNoTracking().Where(a => a.UserId == userId).ToListAsync(Ct);
        Assert.Equal(2, left.Count);
        Assert.All(left, a => Assert.True(a.OccurredAt >= now.AddDays(-89).AddSeconds(-1)));
    }
}
