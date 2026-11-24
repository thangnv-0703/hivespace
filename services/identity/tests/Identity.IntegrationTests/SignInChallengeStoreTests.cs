using Identity.Application.Abstractions;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>FEAT-006 T4: the challenge store and its conflict mapping against a real Postgres.</summary>
public sealed class SignInChallengeStoreTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_second_open_challenge_for_one_address_raises_the_challenge_conflict()
    {
        var emailHash = Guid.NewGuid().ToByteArray();
        var now = DateTimeOffset.UtcNow;
        await using (var db = postgres.CreateContext())
        {
            var store = new EfUserRepository(db);
            ((ISignInChallengeStore)store).Add(SignInCodeChallenge.Issue(Guid.CreateVersion7(now), emailHash, null, [1], now));
            await store.SaveChangesAsync(Ct);
        }

        await using var second = postgres.CreateContext();
        var repository = new EfUserRepository(second);
        ((ISignInChallengeStore)repository).Add(SignInCodeChallenge.Issue(Guid.CreateVersion7(now), emailHash, null, [2], now));

        await Assert.ThrowsAsync<SignInChallengeConflictException>(() => repository.SaveChangesAsync(Ct));
        Assert.Empty(second.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Finds_the_newest_and_the_open_challenges_of_an_address()
    {
        var emailHash = Guid.NewGuid().ToByteArray();
        var now = DateTimeOffset.UtcNow;
        var older = SignInCodeChallenge.Issue(Guid.CreateVersion7(now), emailHash, null, [1], now.AddMinutes(-2));
        older.Supersede(now.AddMinutes(-1));
        var newer = SignInCodeChallenge.Issue(Guid.CreateVersion7(now), emailHash, null, [2], now.AddMinutes(-1));
        await using (var db = postgres.CreateContext())
        {
            db.SignInCodeChallenges.AddRange(older, newer);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = postgres.CreateContext();
        ISignInChallengeStore store = new EfUserRepository(read);

        Assert.Equal(newer.Id, (await store.FindNewestAsync(emailHash, Ct))!.Id);
        Assert.Equal([newer.Id], (await store.FindOpenAsync(emailHash, Ct)).Select(c => c.Id));
        var found = await store.FindAsync(older.Id, Ct);
        Assert.Equal(ChallengeEndReason.Superseded, found!.EndReason);
    }

    [Fact]
    public async Task A_concurrent_change_to_a_challenge_raises_the_concurrency_conflict()
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = SignInCodeChallenge.Issue(Guid.CreateVersion7(now), Guid.NewGuid().ToByteArray(), null, [1], now);
        await using (var db = postgres.CreateContext())
        {
            db.SignInCodeChallenges.Add(challenge);
            await db.SaveChangesAsync(Ct);
        }

        await using var first = postgres.CreateContext();
        await using var second = postgres.CreateContext();
        var a = await first.SignInCodeChallenges.SingleAsync(c => c.Id == challenge.Id, Ct);
        var b = await second.SignInCodeChallenges.SingleAsync(c => c.Id == challenge.Id, Ct);
        a.RecordWrongCode(now);
        b.RecordWrongCode(now);
        await new EfUserRepository(first).SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new EfUserRepository(second).SaveChangesAsync(Ct));
    }
}
