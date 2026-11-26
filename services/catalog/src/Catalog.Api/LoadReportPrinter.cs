using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;

namespace Catalog.Api;

/// <summary>Prints the operator's load report (design §3.4): result, counts per kind, and every problem of a refused load.</summary>
public static class LoadReportPrinter
{
    public static void Print(LoadReport report, TextWriter output)
    {
        output.WriteLine($"Category tree load · source {report.SourcePath} · load {report.LoadId}");
        output.WriteLine(report.Outcome switch
        {
            LoadOutcome.Applied => "Result: APPLIED",
            LoadOutcome.Unchanged => "Result: UNCHANGED",
            _ => $"Result: REFUSED, {report.Problems.Count} problem{(report.Problems.Count == 1 ? string.Empty : "s")}",
        });

        if (report.Outcome != LoadOutcome.Refused)
        {
            var c = report.Counts;
            output.WriteLine($"{string.Empty,-17}{"added",7}{"renamed",9}{"loosened",10}{"reordered",11}{"unchanged",11}{"total",8}");
            Row(output, "categories", c.Categories);
            Row(output, "leaves", c.Leaves);
            Row(output, "attributes", c.Attributes);
            Row(output, "list values", c.ListValues);
            output.WriteLine($"images (top level): {c.Images.Added} added, {c.Images.Changed} changed, {c.Images.Removed} removed, {c.Images.Unchanged} unchanged");
        }

        if (report.Problems.Count > 0)
        {
            output.WriteLine("Problems, one per line:");
            foreach (var problem in report.Problems)
            {
                output.WriteLine($"  {problem}");
            }
        }
    }

    private static void Row(TextWriter output, string label, CountRow row) =>
        output.WriteLine($"{label,-17}{Dash(row.Added),7}{Dash(row.Renamed),9}{Dash(row.Loosened),10}{Dash(row.Reordered),11}{row.Unchanged,11}{row.Total,8}");

    private static string Dash(int value) => value == 0 ? "–" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
