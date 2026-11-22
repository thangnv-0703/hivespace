using System.Security.Claims;
using HiveSpace.BuildingBlocks.Errors;
using Identity.Application.Features.Registration;
using Identity.Domain;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Api.Endpoints;

public sealed record RegisterUserRequest(string? Email, string? DisplayName, string? Password);

public sealed record VerifyEmailRequest(string? Token);

public sealed record ResendVerificationEmailRequest(string? Email);

public sealed record SignInRequest(string? Email, string? Password, string? ReturnUrl);

/// <summary>REST endpoints for <c>contracts/openapi/identity.yaml</c> (design §3.1). Thin: they call Application handlers.</summary>
public static class UsersEndpoints
{
    public const string CookieScheme = "hs.idp";

    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/users", async (HttpContext http, RegisterUserRequest body, RegisterUserHandler handler, CancellationToken ct) =>
            {
                var language = LanguageResolver.Resolve(http.Request.Headers.AcceptLanguage.ToString());
                var result = await handler.HandleAsync(new RegisterUserCommand(body.Email, body.DisplayName, body.Password, language), ct);
                return Results.Created($"/v1/users/{result.OrThrow()}", new { userId = result.Value });
            })
            .WithName("registerUser");

        app.MapPost("/v1/users/verify-email", async (VerifyEmailRequest body, VerifyEmailHandler handler, CancellationToken ct) =>
            {
                (await handler.HandleAsync(body.Token, ct)).OrThrow();
                return Results.NoContent();
            })
            .WithName("verifyEmail");

        app.MapPost("/v1/users/verification-email", async (ResendVerificationEmailRequest body, ResendVerificationEmailHandler handler, CancellationToken ct) =>
            {
                (await handler.HandleAsync(body.Email, ct)).OrThrow();
                return Results.Accepted();
            })
            .WithName("resendVerificationEmail");

        app.MapPost("/v1/users/sign-in", async (HttpContext http, SignInRequest body, SignInHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(new SignInCommand(body.Email, body.Password, body.ReturnUrl), ct);
                var outcome = result.OrThrow();
                await http.SignInAsync(CookieScheme, ToPrincipal(outcome.User));
                return Results.Ok(new { redirectUrl = outcome.RedirectUrl });
            })
            .WithName("signIn");

        return app;
    }

    /// <summary>The Identity session (cookie <c>hs.idp</c>); OpenIddict reads these claims when issuing tokens (T7).</summary>
    public static ClaimsPrincipal ToPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.EmailAddress),
            new("email_verified", user.IsEmailVerified ? "true" : "false"),
            new("name", user.DisplayName),
            new("lang", user.Language),
        };
        claims.AddRange(user.Roles.Select(r => new Claim("role", r.Role.ToString())));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme, "name", "role"));
    }
}
