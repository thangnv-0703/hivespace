namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when an external login insert hits one of its unique indexes:
/// two first sign-ins for one identity raced, or an account already holds an identity of the provider (AC-11).
/// </summary>
public sealed class ExternalLoginConflictException(Exception inner) : Exception("The external login is already linked.", inner);
