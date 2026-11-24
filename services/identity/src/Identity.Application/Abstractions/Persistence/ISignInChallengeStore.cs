using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>Sign-in code challenges (FR-AUTH-028). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface ISignInChallengeStore
{
    Task<SignInCodeChallenge?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The most recently created challenge for the address, in any state.</summary>
    Task<SignInCodeChallenge?> FindNewestAsync(byte[] emailHash, CancellationToken cancellationToken);

    /// <summary>The challenges of the address that have not ended.</summary>
    Task<IReadOnlyList<SignInCodeChallenge>> FindOpenAsync(byte[] emailHash, CancellationToken cancellationToken);

    void Add(SignInCodeChallenge challenge);
}
