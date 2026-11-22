using HiveSpace.BuildingBlocks.Abstractions;
using Xunit;

namespace HiveSpace.BuildingBlocks.Abstractions.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Ok_carries_the_value_and_no_failure()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Fail_carries_the_code_and_every_violation()
    {
        Violation[] violations = [new("email", "identity.email_invalid"), new("password", "identity.password_too_short")];

        var result = Result<Unit>.Fail(ErrorCodes.ValidationFailed, violations);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.Failure!.Code);
        Assert.Equal(violations, result.Failure.Violations);
    }

    [Fact]
    public void Fail_without_violations_has_none()
    {
        var result = Result<string>.Fail(ErrorCodes.Unauthenticated);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Failure!.Violations);
    }
}
