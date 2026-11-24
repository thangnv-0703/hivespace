namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the unique index on <c>shops.owner_user_id</c> rejects an insert:
/// the user registered a shop in a parallel request (FR-SHOP-002).
/// </summary>
public sealed class ShopAlreadyOwnedException(Exception inner) : Exception("The user already owns a shop.", inner);
