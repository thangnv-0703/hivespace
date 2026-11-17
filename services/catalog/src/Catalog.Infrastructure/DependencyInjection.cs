using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Application.Features.Ping;
using Catalog.Infrastructure.GrpcClients;
using Catalog.Infrastructure.Persistence;
using HiveSpace.BuildingBlocks.Messaging;
using HiveSpace.Contracts.Inventory.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Connection names match the AppHost resources (apphost/Resources).</summary>
    public const string DatabaseConnection = "catalogdb";
    public const string MessagingConnection = "messaging";
    public const string EventsTopic = "hivespace-events";

    public static IHostApplicationBuilder AddCatalogInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<CatalogDbContext>(DatabaseConnection);
        builder.AddEventBus<CatalogDbContext>(MessagingConnection, new MessagingOptions(EventsTopic, "catalog"));

        // "_grpc" selects the AppHost endpoint named grpc on the inventory resource (service discovery).
        builder.Services
            .AddGrpcClient<InventoryService.InventoryServiceClient>(o => o.Address = new Uri(
                builder.Configuration["Inventory:GrpcAddress"] ?? "http://_grpc.inventory"));

        builder.Services.AddScoped<ICategoryReadStore, CategoryReadStore>();
        builder.Services.AddScoped<IEventOutbox, EfEventOutbox>();
        builder.Services.AddScoped<IInventoryClient, InventoryClient>();
        builder.Services.AddScoped<ListCategoriesHandler>();
        builder.Services.AddScoped<PublishPingHandler>();
        return builder;
    }
}
