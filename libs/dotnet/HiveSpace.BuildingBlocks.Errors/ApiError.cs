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

public static partial class ErrorHandlingExtensions
{
    /// <summary>
    /// Turns unhandled exceptions into the error envelope. Messages are English placeholders in the skeleton;
    /// localized messages from <c>contracts/errors/messages.*.yaml</c> arrive with FEAT-001.
    /// </summary>
    public static WebApplication UseApiErrors(this WebApplication app)
    {
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ApiErrors");
            if (feature?.Error is { } error)
            {
                LogUnhandled(logger, error);
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ApiError(ErrorCodes.Internal, "Something went wrong. Please try again later."));
        }));

        return app;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception);
}
