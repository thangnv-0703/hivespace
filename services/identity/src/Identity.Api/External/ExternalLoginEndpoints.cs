using Identity.Api.Endpoints;
using Identity.Application.Features.ExternalLogin;
using Identity.Application.Features.Registration;
using HiveSpace.BuildingBlocks.Errors;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Identity.Api.External;

/// <summary>
/// The browser endpoints of external sign-in (FEAT-004 design §3.1). <c>callback</c> belongs to the OpenID Connect handler;
/// <c>start</c> challenges it and <c>complete</c> makes the account decision and starts the Identity session (D-2).
/// </summary>
public static partial class ExternalLoginEndpoints
{
    public static IEndpointRouteBuilder MapExternalLoginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/connect/external/{provider}/start", StartAsync);
        app.MapGet("/connect/external/{provider}/complete", CompleteAsync);
        return app;
    }

    private static async Task<IResult> StartAsync(
        string provider,
        string? returnUrl,
        HttpContext http,
        ExternalLoginProviders providers,
        IOptionsMonitor<OpenIdConnectOptions> oidc,
        ILoggerFactory loggers)
    {
        if (!ExternalLoginProviders.IsKnown(provider))
        {
            return Results.NotFound();
        }

        var safeReturnUrl = SafeReturnUrl(returnUrl);
        var logger = loggers.CreateLogger(typeof(ExternalLoginEndpoints));
        if (providers.Find(provider) is not { } registered)
        {
            LogOutcome(logger, provider, ExternalFailure.Unavailable, null);
            return Results.Redirect(ExternalFailure.SignInUrl(ExternalFailure.Unavailable, safeReturnUrl));
        }

        // Fetch (or reuse) the provider metadata first, under the timeout, so a provider that is down gives the unavailable
        // redirect instead of a 500 from the challenge (UC-025 1a, NFR-REL-008).
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
            timeout.CancelAfter(ExternalLoginSetup.ProviderTimeout);
            await oidc.Get(registered.Scheme).ConfigurationManager!.GetConfigurationAsync(timeout.Token);
        }
        catch (Exception ex) when (!http.RequestAborted.IsCancellationRequested)
        {
            LogOutcome(logger, provider, ExternalFailure.Unavailable, ex.GetType().Name);
            return Results.Redirect(ExternalFailure.SignInUrl(ExternalFailure.Unavailable, safeReturnUrl));
        }

        var properties = new AuthenticationProperties { RedirectUri = ExternalLoginSetup.CompletePath(provider) };
        if (safeReturnUrl is not null)
        {
            properties.Items[ExternalLoginSetup.ReturnUrlKey] = safeReturnUrl;
        }

        return Results.Challenge(properties, [registered.Scheme]);
    }

    private static async Task<IResult> CompleteAsync(
        string provider,
        HttpContext http,
        ExternalLoginProviders providers,
        ExternalSignInHandler handler,
        ILoggerFactory loggers,
        CancellationToken ct)
    {
        if (!ExternalLoginProviders.IsKnown(provider))
        {
            return Results.NotFound();
        }

        var logger = loggers.CreateLogger(typeof(ExternalLoginEndpoints));

        // hs.ext is read once: it is cleared before anything else, so a replayed complete finds nothing (AC-6).
        var external = await http.AuthenticateAsync(ExternalLoginSetup.ExternalCookieScheme);
        await http.SignOutAsync(ExternalLoginSetup.ExternalCookieScheme);
        var returnUrl = SafeReturnUrl(external.Properties?.GetString(ExternalLoginSetup.ReturnUrlKey));
        if (!external.Succeeded || providers.Find(provider) is not { } registered
            || external.Principal?.Identity?.AuthenticationType != registered.Scheme)
        {
            LogOutcome(logger, provider, ExternalFailure.Failed, null);
            return Results.Redirect(ExternalFailure.SignInUrl(ExternalFailure.Failed, returnUrl));
        }

        var language = LanguageResolver.Resolve(http.Request.Headers.AcceptLanguage.ToString());
        var result = await handler.HandleAsync(new ExternalSignInCommand(registered.Map(external.Principal), language), ct);
        if (!result.IsSuccess)
        {
            LogOutcome(logger, provider, result.Failure!.Code, null);
            return Results.Redirect(ExternalFailure.SignInUrl(result.Failure.Code, returnUrl));
        }

        var outcome = result.Value!;
        await http.SignInAsync(UsersEndpoints.CookieScheme, UsersEndpoints.ToPrincipal(outcome.User, outcome.PasswordStamp));
        LogSignedIn(logger, provider, outcome.User.Id);
        return Results.Redirect(returnUrl ?? "/");
    }

    /// <summary>The <c>returnUrl</c> when it is a local <c>/connect/authorize…</c> path, otherwise null (AC-10, FEAT-001 AC-17).</summary>
    internal static string? SafeReturnUrl(string? returnUrl) =>
        SignInHandler.SafeRedirect(returnUrl) is var safe && safe != "/" ? safe : null;

    // Provider, outcome and user id only (NFR-SEC-008, design §5.2).
    [LoggerMessage(Level = LogLevel.Information, Message = "External sign-in with {Provider} failed: {Outcome} ({Reason})")]
    private static partial void LogOutcome(ILogger logger, string provider, string outcome, string? reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "External sign-in with {Provider} succeeded for user {UserId}")]
    private static partial void LogSignedIn(ILogger logger, string provider, Guid userId);
}
