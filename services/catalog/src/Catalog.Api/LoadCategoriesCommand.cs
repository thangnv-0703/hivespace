using Catalog.Application.Features.Categories;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api;

/// <summary>
/// <c>Catalog.Api load-categories --source &lt;dir&gt;</c>: the operator's category load (design §3.4, D-3).
/// Exit codes: 0 applied or unchanged, 2 refused (problems listed), 1 unexpected failure (nothing committed).
/// </summary>
public static class LoadCategoriesCommand
{
    public const string Name = "load-categories";

    public static async Task<int> RunAsync(string[] args)
    {
        var source = SourceArgument(args);
        if (source is null)
        {
            await Console.Error.WriteLineAsync($"Usage: Catalog.Api {Name} --source <directory>").ConfigureAwait(false);
            return 1;
        }

        try
        {
            // The same host as the service, without endpoints or Kestrel and without starting hosted services, so the
            // outbox relay stays off and the run ends promptly. The event leaves the outbox when Catalog next runs (ADR-0002).
            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();
            builder.AddCatalogInfrastructure();
            await using var app = builder.Build();

            await using var scope = app.Services.CreateAsyncScope();
            if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
            {
                await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync().ConfigureAwait(false);
            }

            var report = await scope.ServiceProvider.GetRequiredService<LoadCategoryTreeHandler>()
                .HandleAsync(Path.GetFullPath(source), CancellationToken.None).ConfigureAwait(false);
            LoadReportPrinter.Print(report, Console.Out);
            return report.Outcome == LoadOutcome.Refused ? 2 : 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Category tree load failed, nothing was committed: {ex}").ConfigureAwait(false);
            return 1;
        }
    }

    private static string? SourceArgument(string[] args)
    {
        var index = Array.IndexOf(args, "--source");
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
