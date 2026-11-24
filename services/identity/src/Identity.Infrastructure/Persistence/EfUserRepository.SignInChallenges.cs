using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository
{
    Task<SignInCodeChallenge?> ISignInChallengeStore.FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.SignInCodeChallenges.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<SignInCodeChallenge?> FindNewestAsync(byte[] emailHash, CancellationToken cancellationToken) =>
        db.SignInCodeChallenges
            .Where(c => c.EmailHash == emailHash)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SignInCodeChallenge>> FindOpenAsync(byte[] emailHash, CancellationToken cancellationToken) =>
        await db.SignInCodeChallenges
            .Where(c => c.EmailHash == emailHash && c.EndedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(SignInCodeChallenge challenge) => db.SignInCodeChallenges.Add(challenge);
}
