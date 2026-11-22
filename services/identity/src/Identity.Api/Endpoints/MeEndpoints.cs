using System.Security.Claims;
using System.Text.Json;
using HiveSpace.BuildingBlocks.Abstractions;
using HiveSpace.BuildingBlocks.Errors;
using Identity.Application.Features.Profile;
using Microsoft.AspNetCore.Authorization;

namespace Identity.Api.Endpoints;

/// <summary>
/// The signed-in user's own profile (<c>contracts/openapi/identity.yaml</c>, technical-design §3.1). The user is always the
/// <c>sub</c> of the validated bearer token: no id in the path, and <c>X-User-Id</c> or a body field never picks the user (D-5).
/// </summary>
public static class MeEndpoints
{
    /// <summary>Authorization policy that accepts only the bearer token, never the <c>hs.idp</c> cookie.</summary>
    public const string Policy = "me";

    private static readonly string[] OptionalKeys = ["phone", "gender", "dateOfBirth"];
    private static readonly HashSet<string> KnownKeys = ["displayName", "phone", "gender", "dateOfBirth"];

    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/v1/me").RequireAuthorization(Policy);

        me.MapGet("/profile", async (ClaimsPrincipal user, GetMyProfileHandler handler, CancellationToken ct) =>
                Results.Ok((await handler.HandleAsync(user.GetUserId(), ct)).OrThrow()))
            .WithName("getMyProfile");

        me.MapPut("/profile", async (ClaimsPrincipal user, JsonElement body, UpdateMyProfileHandler handler, CancellationToken ct) =>
            {
                var command = ToCommand(user.GetUserId(), body);
                return Results.Ok((await handler.HandleAsync(command, ct)).OrThrow());
            })
            .WithName("updateMyProfile");

        me.MapPut("/profile/language", async (ClaimsPrincipal user, JsonElement body, UpdateMyLanguageHandler handler, CancellationToken ct) =>
            {
                var language = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("language", out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
                var saved = (await handler.HandleAsync(new UpdateMyLanguageCommand(user.GetUserId(), language), ct)).OrThrow();
                return Results.Ok(new { language = saved });
            })
            .WithName("updateMyLanguage");

        return app;
    }

    /// <summary>
    /// Reads the body as a <see cref="JsonElement"/> so an unknown key (notably <c>email</c>) becomes a field error instead of being
    /// dropped. A missing or wrongly typed key is a field error too: PUT carries the whole editable representation.
    /// </summary>
    private static UpdateMyProfileCommand ToCommand(Guid userId, JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw ApiException.Validation([new FieldError("body", ErrorCodes.ValidationFailed)]);
        }

        var invalid = body.EnumerateObject().Select(p => p.Name).Where(name => !KnownKeys.Contains(name)).ToList();
        invalid.AddRange(OptionalKeys.Where(key => !body.TryGetProperty(key, out _)));

        string? Read(string key)
        {
            if (!body.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (!invalid.Contains(key))
            {
                invalid.Add(key);
            }

            return null;
        }

        return new UpdateMyProfileCommand(userId, Read("displayName"), Read("phone"), Read("gender"), Read("dateOfBirth"), invalid);
    }
}
