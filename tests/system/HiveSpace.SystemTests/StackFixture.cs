using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

[assembly: AssemblyFixture(typeof(HiveSpace.SystemTests.StackFixture))]

namespace HiveSpace.SystemTests;

/// <summary>
/// Boots the whole AppHost once for the run (repo-structure §5 "Tests"). Needs Docker, Go, uv and pnpm,
/// exactly like `dotnet run --project apphost/HiveSpace.AppHost`.
/// </summary>
public sealed class StackFixture : IAsyncLifetime
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    public static readonly string[] Services =
        ["gateway", "catalog", "identity", "order", "payment", "media", "inventory", "cart", "notification", "search-ai"];

    public DistributedApplication App { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.HiveSpace_AppHost>(cts.Token);
        App = await builder.BuildAsync(cts.Token);
        await App.StartAsync(cts.Token);
        foreach (var service in Services)
        {
            await App.ResourceNotifications.WaitForResourceHealthyAsync(service, cts.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}
