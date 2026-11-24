using Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed partial class EfUserRepository
{
    public Task<Shop?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
        db.Shops.FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
        db.Shops.AnyAsync(s => s.NormalizedName == normalizedName, cancellationToken);

    public void Add(Shop shop) => db.Shops.Add(shop);
}
