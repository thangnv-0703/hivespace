using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository
{
    public Task<SignInLockout?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.SignInLockouts.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

    public void Add(SignInLockout lockout) => db.SignInLockouts.Add(lockout);

    public void Remove(SignInLockout lockout) => db.SignInLockouts.Remove(lockout);
}
