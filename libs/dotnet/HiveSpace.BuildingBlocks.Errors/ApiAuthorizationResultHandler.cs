using HiveSpace.BuildingBlocks.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace HiveSpace.BuildingBlocks.Errors;

/// <summary>Writes the standard error envelope for 401 and 403 from authorization, in the request language (FR-LOC-004).</summary>
public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (authorizeResult.Challenged)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return ApiErrorResponder.WriteAsync(context, new ApiException(ErrorCodes.Unauthenticated));
        }

        if (authorizeResult.Forbidden)
        {
            return ApiErrorResponder.WriteAsync(context, new ApiException(ErrorCodes.Forbidden));
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
