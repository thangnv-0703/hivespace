namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when a credential insert hits the primary key: the account got a
/// password between the check and the save (two parallel set-password calls, FR-AUTH-027).
/// </summary>
public sealed class CredentialExistsException(Exception inner) : Exception("The account already has a password.", inner);
