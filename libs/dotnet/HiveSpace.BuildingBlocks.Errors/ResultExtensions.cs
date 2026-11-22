using HiveSpace.BuildingBlocks.Abstractions;

namespace HiveSpace.BuildingBlocks.Errors;

public static class ResultExtensions
{
    /// <summary>
    /// Returns the value of a successful result, or throws an <see cref="ApiException"/> with the failure code and one
    /// <see cref="FieldError"/> per <see cref="Violation"/>; <see cref="ApiErrorResponder"/> turns it into the error envelope.
    /// </summary>
    public static T OrThrow<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? result.Value!
            : throw new ApiException(
                result.Failure!.Code,
                result.Failure.Violations?.Select(v => new FieldError(v.Field, v.Code)).ToList());
    }
}
