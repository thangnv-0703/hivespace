using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Catalog.ArchitectureTests;

/// <summary>
/// Rich-shape rules (repo-structure §4.1, dev-standards §8.1):
/// Api → Application → Domain, Infrastructure → Application; Domain references nothing.
/// </summary>
public sealed class LayerRulesTests
{
    private static readonly Assembly Domain = typeof(Catalog.Domain.Categories.Category).Assembly;
    private static readonly Assembly Application = typeof(Catalog.Application.Features.Categories.ListCategoriesHandler).Assembly;

    private static readonly string[] FrameworkAndVendors =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "Grpc",
        "Google.Protobuf",
        "Azure",
    ];

    [Fact]
    public void Domain_has_no_framework_or_vendor_dependencies()
    {
        var result = Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny(FrameworkAndVendors).GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Domain_does_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny("Catalog.Application", "Catalog.Infrastructure", "Catalog.Api")
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_api_or_vendors()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot().HaveDependencyOnAny([.. FrameworkAndVendors, "Catalog.Infrastructure", "Catalog.Api"])
            .GetResult();
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
