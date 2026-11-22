using Gateway.Auth;
using Gateway.Proxy;
using Gateway.Spa;
using HiveSpace.BuildingBlocks.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddGatewayAuth();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(GatewayTransforms.Apply)
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseApiErrors();
app.MapDefaultEndpoints();

// Production: the built SPAs are copied into wwwroot by the Dockerfile and served one per host (ADR-0027).
// Development: the "spa-*" proxy routes forward to the Vite dev servers the AppHost starts, matched by host the same way.
app.UseSpaStaticFiles();
app.UseGatewayAuth();

app.MapBffEndpoints();
app.MapReverseProxy();

app.MapSpaFallbacks();

await app.RunAsync();

/// <summary>Entry point marker for WebApplicationFactory in tests.</summary>
public partial class Program;
