using NetArchTest.Rules;
using Xunit;

namespace HiveSpace.BuildingBlocks.Abstractions.Tests;

/// <summary>Domain and Application layers reference this lib, so it must stay free of frameworks and vendors (ADR-0028).</summary>
public sealed class DependencyRulesTests
{
    [Fact]
    public void Abstractions_depend_on_no_framework_or_vendor()
    {
        var result = Types.InAssembly(typeof(ErrorCodes).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft", "Npgsql", "Azure", "Grpc", "Google.Protobuf")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
