using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiveSpace.BuildingBlocks.Errors;

/// <summary>A field-level problem inside an <see cref="ApiError"/>.</summary>
public sealed record FieldError(string Field, string Code, string? Message = null);

/// <summary>The error envelope every REST rejection uses: <c>{ code, message, details[] }</c> (dev-standards §5, FR-LOC-004).</summary>
public sealed record ApiError(string Code, string Message, IReadOnlyList<FieldError>? Details = null);

/// <summary>Stable error codes shared by all .NET services; the full list is <c>contracts/errors/error-codes.yaml</c>.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "common.validation_failed";
    public const string Unauthenticated = "common.unauthenticated";
    public const string Forbidden = "common.forbidden";
    public const string NotFound = "common.not_found";
    public const string Internal = "common.internal";
    public const string DependencyUnavailable = "common.dependency_unavailable";
}

/// <summary>Picks the response language from <c>Accept-Language</c>: <c>vi</c> (default) or <c>en</c> (D-2).</summary>
public static class LanguageResolver
{
    public static string Resolve(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return MessageCatalog.DefaultLanguage;
        }

        foreach (var part in acceptLanguage.Split(',', StringSplitOptions.TrimEntries))
        {
            var tag = part.Split(';')[0].Trim();
            var primary = tag.Split('-')[0].ToLowerInvariant();
            if (primary is "vi" or "en")
            {
                return primary;
            }
        }

        return MessageCatalog.DefaultLanguage;
    }
}

/// <summary>Writes the error envelope: expected failures get their registered status and a localized message; anything else is a 500.</summary>
public static partial class ApiErrorResponder
{
    public static async Task WriteAsync(HttpContext context, Exception? exception)
    {
        var language = LanguageResolver.Resolve(context.Request.Headers.AcceptLanguage.ToString());

        if (exception is ApiException api)
        {
            var details = api.Details?
                .Select(d => d with { Message = d.Message ?? MessageCatalog.MessageOf(d.Code, language) })
                .ToList();
            context.Response.StatusCode = MessageCatalog.StatusOf(api.Code);
            await context.Response.WriteAsJsonAsync(
                new ApiError(api.Code, MessageCatalog.MessageOf(api.Code, language), details));
            return;
        }

        if (exception is not null)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiErrors");
            LogUnhandled(logger, exception);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(
            new ApiError(ErrorCodes.Internal, MessageCatalog.MessageOf(ErrorCodes.Internal, language)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}

public static class ErrorHandlingExtensions
{
    /// <summary>Turns <see cref="ApiException"/> into a localized 4xx envelope and unhandled exceptions into a 500 envelope.</summary>
    public static WebApplication UseApiErrors(this WebApplication app)
    {
        app.UseExceptionHandler(handler => handler.Run(context =>
            ApiErrorResponder.WriteAsync(context, context.Features.Get<IExceptionHandlerFeature>()?.Error)));

        return app;
    }
}
