using System.Security.Claims;
using HiveSpace.BuildingBlocks.Errors;
using Identity.Application.Features.CodeSignIn;
using Identity.Application.Features.Registration;
using Identity.Domain;
using Microsoft.AspNetCore.Authentication;

namespace Identity.Api.Endpoints;

public sealed record RegisterUserRequest(string? Email, string? DisplayName, string? Password);

public sealed record VerifyEmailRequest(string? Token);

public sealed record ResendVerificationEmailRequest(string? Email);

public sealed record SignInRequest(string? Email, string? Password, string? ReturnUrl);

public sealed record SignInCodeRequest(string? Email);

/// <summary>REST endpoints for <c>contracts/openapi/identity.yaml</c> (design §3.1). Thin: they call Application handlers.</summary>
public static partial class UsersEndpoints
{
    public const string CookieScheme = "hs.idp";

    /// <summary>The credential's <c>updated_at</c> as Unix ms: a cookie issued before a password change no longer signs in silently (D-4).</summary>
    public const string PasswordStampClaim = "pwd_at"; // pragma: allowlist secret - a claim name, not a credential

    /// <summary>Unix seconds of the interactive sign-in behind the session (OIDC <c>auth_time</c>, FEAT-005 §3.6).</summary>
    public const string AuthTimeClaim = "auth_time";

    /// <summary>How the user signed in (OIDC <c>amr</c>): <c>pwd</c> or <c>google</c>.</summary>
    public const string AmrClaim = "amr";

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
                await http.SignInAsync(CookieScheme, ToPrincipal(outcome.User, outcome.PasswordStamp, outcome.AuthTime.ToUnixTimeSeconds(), "pwd"));
                return Results.Ok(new { redirectUrl = outcome.RedirectUrl });
            })
            .WithName("signIn");

        app.MapPost("/v1/users/sign-in/code-challenges", async (SignInCodeRequest body, RequestSignInCodeHandler handler, ILoggerFactory loggers, CancellationToken ct) =>
            {
                switch ((await handler.HandleAsync(new RequestSignInCodeCommand(body.Email), ct)).OrThrow())
                {
                    case SignInCodeRequestOutcome.TooSoon tooSoon:
                        throw new ApiException(SignInCodeTooSoon) { RetryAfterSeconds = tooSoon.RetryAfterSeconds };
                    case SignInCodeRequestOutcome.Issued issued:
                        // The challenge id only: never the email, the code, or whether an account exists (NFR-SEC-016, design §5.2).
                        LogCodeIssued(loggers.CreateLogger(typeof(UsersEndpoints)), issued.ChallengeId);
                        return Results.Json(
                            new { challengeId = issued.ChallengeId, expiresAt = issued.ExpiresAt, resendAvailableAt = issued.ResendAvailableAt },
                            statusCode: StatusCodes.Status201Created);
                    default:
                        throw new InvalidOperationException("Unknown outcome.");
                }
            })
            .WithName("requestSignInCode");

        return app;
    }

    private const string SignInCodeTooSoon = "identity.sign_in_code_too_soon";

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in code issued: challenge {ChallengeId}")]
    private static partial void LogCodeIssued(ILogger logger, Guid challengeId);

    /// <summary>The <c>auth_time</c> of a cookie or token principal, or null when it has none or it is not a number.</summary>
    public static long? AuthTimeOf(ClaimsPrincipal? principal) =>
        long.TryParse(principal?.FindFirstValue(AuthTimeClaim), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>The Identity session (cookie <c>hs.idp</c>); OpenIddict reads these claims when issuing tokens (T7).</summary>
    public static ClaimsPrincipal ToPrincipal(User user, long? passwordStamp = null, long? authTime = null, string? amr = null)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.EmailAddress),
            new("email_verified", user.IsEmailVerified ? "true" : "false"),
            new("name", user.DisplayName),
            new("lang", user.Language),
        };
        if (passwordStamp is { } stamp)
        {
            claims.Add(new Claim(PasswordStampClaim, stamp.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (authTime is { } at)
        {
            claims.Add(new Claim(AuthTimeClaim, at.ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        }

        if (amr is not null)
        {
            claims.Add(new Claim(AmrClaim, amr));
        }

        claims.AddRange(user.Roles.Select(r => new Claim("role", r.Role.ToString())));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme, "name", "role"));
    }
}
