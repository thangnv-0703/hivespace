using HiveSpace.BuildingBlocks.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.UseApiErrors();
app.MapDefaultEndpoints();

// Template sample slice (repo-structure §9). STUB: replaced by the service's first feature slice.
app.MapGet("/v1/payment/ping", () => Results.Ok(new { service = "payment" }))
    .WithName("pingPayment");

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
