namespace HiveSpace.BuildingBlocks.Abstractions;

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
