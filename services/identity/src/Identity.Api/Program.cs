using HiveSpace.BuildingBlocks.Errors;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddIdentityInfrastructure();

var app = builder.Build();

app.UseApiErrors();
app.MapDefaultEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
{
    // Local and test only: the AppHost runs migrations on start (architecture §8.2). Cloud uses a deploy step.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

// Template sample slice (repo-structure §9). STUB: replaced by the service's first feature slice.
app.MapGet("/v1/identity/ping", () => Results.Ok(new { service = "identity" }))
    .WithName("pingIdentity");

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
