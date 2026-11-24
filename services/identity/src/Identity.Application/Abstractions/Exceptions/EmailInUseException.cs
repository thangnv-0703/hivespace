namespace Identity.Application.Abstractions;

/// <summary>Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two registrations race for one normalized email.</summary>
public sealed class EmailInUseException(Exception inner) : Exception("The email is already registered.", inner);
