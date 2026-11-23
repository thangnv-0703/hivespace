using Aspire.Hosting;
using System.Collections.Concurrent;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private readonly ConcurrentQueue<(string Resource, string Line)> _logs = new();

    public DistributedApplication App { get; private set; } = null!;

    /// <summary>Everything a resource wrote to its console since the stack started (the host forwards it to ILogger).</summary>
    public IReadOnlyList<string> LogsOf(string resource) => [.. _logs.Where(l => l.Resource == resource).Select(l => l.Line)];

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);
        // The suite signs in many users from one loopback address; the real gateway limit is 10 per minute (FEAT-005 D-9, scaffold-notes).
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.HiveSpace_AppHost>(["RateLimits:PermitLimit=1000"], cts.Token);
        builder.Services.AddLogging(logging => logging.AddProvider(new CapturingProvider(_logs)));
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

    private sealed class CapturingProvider(ConcurrentQueue<(string Resource, string Line)> sink) : ILoggerProvider
    {
        private const string Prefix = "HiveSpace.AppHost.Resources.";

        public ILogger CreateLogger(string categoryName) =>
            categoryName.StartsWith(Prefix, StringComparison.Ordinal) ? new Capture(categoryName[Prefix.Length..], sink) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }

        private sealed class Capture(string resource, ConcurrentQueue<(string Resource, string Line)> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                sink.Enqueue((resource, formatter(state, exception)));
        }
    }
}
