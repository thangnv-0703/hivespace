using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;

namespace Identity.Application.Features.Profile;

/// <summary>FR-PROF-001: the profile of the user named by the validated token.</summary>
public sealed class GetMyProfileHandler(IUserRepository users)
{
    public async Task<Result<ProfileView>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null
            ? Result<ProfileView>.Fail(ErrorCodes.Unauthenticated)
            : Result<ProfileView>.Ok(ProfileView.From(user, await users.FindCredentialAsync(userId, cancellationToken).ConfigureAwait(false) is not null));
    }
}
