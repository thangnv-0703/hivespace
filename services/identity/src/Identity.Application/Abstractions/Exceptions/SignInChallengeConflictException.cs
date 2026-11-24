namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when the "one open challenge per address" index rejects an insert:
/// another code request for the address committed first (FEAT-006 §3.2).
/// </summary>
public sealed class SignInChallengeConflictException(Exception inner) : Exception("Another sign-in code was issued for the address.", inner);
