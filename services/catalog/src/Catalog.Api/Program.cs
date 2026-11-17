using Catalog.Api.Endpoints;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Errors;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCatalogInfrastructure();
builder.Services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("catalogdb", tags: ["ready"]);

var app = builder.Build();

app.UseApiErrors();
app.MapDefaultEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    // Local and test only: the AppHost runs migrations on start (architecture §8.2). Cloud uses a deploy step.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
}

app.MapCategoryEndpoints();

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in contract tests.</summary>
public partial class Program;
