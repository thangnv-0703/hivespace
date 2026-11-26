using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Order.Tests;

/// <summary>
/// Rich shape file layout (dev-standards §8.0, repo-structure §4.1). It reads the source files because file layout does
/// not survive compilation, so NetArchTest cannot see it.
/// </summary>
public sealed partial class PortLayoutTests
{
    [Fact]
    public void Each_port_has_its_own_file_named_after_it()
    {
        var application = Path.Combine(SourceRoot(), "Order.Application");
        Assert.True(Directory.Exists(application), $"Not found: {application}");

        // A newly generated service has no ports yet.
        var abstractions = Path.Combine(application, "Abstractions");
        var violations = Directory.Exists(abstractions)
            ? SourceFiles(abstractions)
                .Select(file => (File: file, Ports: InterfaceDeclaration().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).ToArray()))
                .Where(x => x.Ports.Length > 1 || (x.Ports.Length == 1 && x.Ports[0] != Path.GetFileNameWithoutExtension(x.File)))
                .Select(x => $"{Path.GetRelativePath(application, x.File)}: {string.Join(", ", x.Ports)}")
                .ToArray()
            : [];

        Assert.True(violations.Length == 0, "One port per file, named after it: " + string.Join("; ", violations));
    }

    [Fact]
    public void No_catch_all_port_or_adapter_files()
    {
        var violations = SourceFiles(SourceRoot())
            .Where(file => Path.GetFileName(file) is "Ports.cs" or "Adapters.cs")
            .ToArray();

        Assert.True(violations.Length == 0, "Split into one file per port or adapter: " + string.Join("; ", violations));
    }

    private static string SourceRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src"));

    private static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    [GeneratedRegex(@"^[ \t]*(?:(?:public|internal|partial)[ \t]+)*interface[ \t]+(\w+)", RegexOptions.Multiline)]
    private static partial Regex InterfaceDeclaration();
}
