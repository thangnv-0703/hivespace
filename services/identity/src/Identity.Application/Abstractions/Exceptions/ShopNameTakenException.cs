namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the unique index on <c>shops.normalized_name</c> rejects an insert:
/// another shop took the name first (FR-SHOP-003).
/// </summary>
public sealed class ShopNameTakenException(Exception inner) : Exception("The shop name is already taken.", inner);
