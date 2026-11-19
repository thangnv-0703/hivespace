namespace HiveSpace.BuildingBlocks.Errors;

/// <summary>
/// An expected, user-facing failure. Carries a stable code from <c>contracts/errors/error-codes.yaml</c>;
/// <see cref="ApiErrorResponder"/> maps it to its status and a message in the request language.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(string code, IReadOnlyList<FieldError>? details = null)
        : base(code)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public IReadOnlyList<FieldError>? Details { get; }

    /// <summary>A <c>common.validation_failed</c> failure listing every unmet rule at once.</summary>
    public static ApiException Validation(IReadOnlyList<FieldError> details) =>
        new(ErrorCodes.ValidationFailed, details);
}
