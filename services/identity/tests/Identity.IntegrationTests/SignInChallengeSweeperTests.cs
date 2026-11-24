using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-006 T8: AC-12, challenges are purged a day after they expire (D-3).</summary>
public sealed class SignInChallengeSweeperTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ac12_RunOnceAsync_deletes_challenges_expired_more_than_a_day_ago_and_keeps_one_expired_23_hours_ago()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredLongAgo = Challenge(now.AddDays(-2));
        var expiredJustOverADay = Challenge(now.AddDays(-1).AddMinutes(-1));
        var expired23HoursAgo = Challenge(now.AddHours(-23));
        var open = Challenge(now.AddMinutes(5));
        await using (var db = postgres.CreateContext())
        {
            db.SignInCodeChallenges.AddRange(expiredLongAgo, expiredJustOverADay, expired23HoursAgo, open);
            await db.SaveChangesAsync(Ct);
        }

        var services = new ServiceCollection().AddScoped(_ => postgres.CreateContext()).BuildServiceProvider();
        var sweeper = new SignInChallengeSweeper(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<SignInChallengeSweeper>.Instance);

        var deleted = await sweeper.RunOnceAsync(now, Ct);

        Assert.Equal(2, deleted);
        await using var read = postgres.CreateContext();
        var left = await read.SignInCodeChallenges.AsNoTracking().Select(c => c.Id).ToListAsync(Ct);
        Assert.Equal(new[] { expired23HoursAgo.Id, open.Id }.Order(), left.Order());
    }

    // Each challenge gets its own address hash, so the open-challenge index allows them all.
    private static SignInCodeChallenge Challenge(DateTimeOffset expiresAt)
    {
        var issuedAt = expiresAt - SignInCodePolicy.Lifetime;
        return SignInCodeChallenge.Issue(Guid.CreateVersion7(issuedAt), Guid.NewGuid().ToByteArray(), null, [1], issuedAt);
    }
}
