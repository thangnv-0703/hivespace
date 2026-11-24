namespace Identity.Application.Abstractions;

/// <summary>
/// Signs the user out of every session except one (FR-AUTH-024). It only stages changes in the same unit of work;
/// <see cref="IUserRepository.SaveChangesAsync"/> commits them with the credential and the outbox row (D-2).
/// </summary>
public interface ISessionRevoker
{
    /// <param name="keepAuthorizationId">The authorization to keep; null revokes all of them.</param>
    Task RevokeOtherSessionsAsync(Guid userId, string? keepAuthorizationId, CancellationToken cancellationToken);
}
