using System.Security.Claims;
using HiveSpace.BuildingBlocks.Abstractions;

namespace HiveSpace.BuildingBlocks.Errors;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The user id from the <c>sub</c> claim of the validated token; a missing or malformed one is <c>common.unauthenticated</c>.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : throw new ApiException(ErrorCodes.Unauthenticated);
    }
}
