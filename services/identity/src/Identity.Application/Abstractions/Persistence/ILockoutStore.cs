using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>Lockout rows (FR-AUTH-011). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface ILockoutStore
{
    Task<SignInLockout?> FindAsync(Guid userId, CancellationToken cancellationToken);

    void Add(SignInLockout lockout);

    void Remove(SignInLockout lockout);
}
