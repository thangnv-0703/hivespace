using HiveSpace.AppHost.Resources;

// The single entry point for the whole stack (NFR-PORT-001, ADR-0015):
//   dotnet run --project apphost/HiveSpace.AppHost
// Kept thin: each Resources/*.cs file registers one group, in dependency order.
var builder = DistributedApplication.CreateBuilder(args);

var infra = builder.AddInfrastructure();
var go = builder.AddGoServices(infra);
var python = builder.AddPythonServices(infra);
var dotnet = builder.AddDotnetServices(infra, go);
var web = builder.AddWeb();
builder.AddGateway(infra, dotnet, go, python, web);

builder.Build().Run();
