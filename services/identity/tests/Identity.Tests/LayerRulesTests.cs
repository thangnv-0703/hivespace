using NetArchTest.Rules;
using Xunit;

namespace Identity.Tests;

/// <summary>Rich shape: Domain references no framework, vendor or outer layer (repo-structure §4.1).</summary>
public sealed class LayerRulesTests
{
    [Fact]
    public void Domain_is_pure()
    {
        var result = Types.InAssembly(typeof(Identity.Domain.DomainMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "Grpc", "Azure",
                "Identity.Application", "Identity.Infrastructure", "Identity.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api()
    {
        var result = Types.InAssembly(typeof(Identity.Application.ApplicationMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Identity.Infrastructure", "Identity.Api")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }
}
