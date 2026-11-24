namespace Identity.Application.Abstractions;

/// <summary>
/// Raised by <see cref="IUserRepository.SaveChangesAsync"/> when two failed attempts for one account raced: the lockout row changed
/// or was inserted since it was read. Nothing stays staged, so the caller reads it again and reapplies (FEAT-005 D-3).
/// </summary>
public sealed class ConcurrencyConflictException(Exception inner) : Exception("A lockout or challenge changed concurrently.", inner);
