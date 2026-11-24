using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>Shops (FEAT-009). Changes are staged and committed by <see cref="IUserRepository.SaveChangesAsync"/>.</summary>
public interface IShopStore
{
    Task<Shop?> FindByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken);

    void Add(Shop shop);
}
