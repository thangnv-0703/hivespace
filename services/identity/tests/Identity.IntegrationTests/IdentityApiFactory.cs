using System.Security.Cryptography;
using HiveSpace.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>Boots Identity.Api against a real Postgres container; the broker is faked (the relay is removed).</summary>
public sealed class IdentityApiFactory(string? googleAuthority = null, TimeProvider? clock = null) : WebApplicationFactory<Program>
{
    public const string GoogleClientId = "hivespace-google-test";
    public const string GoogleClientSecret = "google-test-secret"; // pragma: allowlist secret - test value
    public const string GoogleCallback = Issuer + "/connect/external/google/callback";

    public const string Issuer = "http://localhost:5080";
    public const string SellerOrigin = "http://seller.localhost:5080";
    public const string ClientSecret = "test-client-secret";
    /// <summary>A fixed 32-byte key (base64) for bank number encryption in tests (FEAT-010 D-1).</summary>
    public const string TestBankKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="; // pragma: allowlist secret - test value
    public const string SellerClientSecret = "test-seller-secret";

    /// <summary>The server's signing key, so tests can mint a token with a changed claim (wrong audience).</summary>
    public ECDsa SigningKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    /// <summary>Every log line the app wrote, after the app's own level filters (the log-capture test, AC-16).</summary>
    public CapturedLogs Logs { get; } = new();

    public async Task StartAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:identitydb", _postgres.GetConnectionString());
        // A syntactically valid emulator string; the outbox relay is removed below so nothing connects.
        builder.UseSetting(
            "ConnectionStrings:messaging",
            "Endpoint=sb://localhost;SharedAccessKeyName=test;SharedAccessKey=test;UseDevelopmentEmulator=true;");
        // Fresh keys per run; production reads them from configuration (D-7).
        builder.UseSetting("Identity:Issuer", Issuer);
        builder.UseSetting("Identity:Clients:Storefront:BaseUrl", Issuer);
        builder.UseSetting("Identity:Clients:Seller:BaseUrl", SellerOrigin);
        builder.UseSetting("Identity:SigningKeyPem", SigningKey.ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:EncryptionKeyPem", RSA.Create(2048).ExportPkcs8PrivateKeyPem());
        builder.UseSetting("Identity:BankEncryption:ActiveKeyId", "v1");
        builder.UseSetting("Identity:BankEncryption:Keys:v1", TestBankKey);
        builder.UseSetting("Identity:Clients:Storefront:ClientSecret", ClientSecret);
        builder.UseSetting("Identity:Clients:Seller:ClientSecret", SellerClientSecret);
        if (googleAuthority is not null)
        {
            builder.UseSetting("Identity:Google:ClientId", GoogleClientId);
            builder.UseSetting("Identity:Google:ClientSecret", GoogleClientSecret);
            builder.UseSetting("Identity:Google:Authority", googleAuthority);
        }

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services =>
        {
            var relay = services.Single(d => d.ImplementationType?.Name.StartsWith("OutboxRelay", StringComparison.Ordinal) == true);
            services.Remove(relay);
            services.RemoveAll<IEventBus>();
            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            }
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Collects formatted log lines (with exception text) in memory.</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public string Text => string.Join('\n', _lines);

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, System.Collections.Concurrent.ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
    }
}

/// <summary>The shell endpoints every service answers (moved here from the template ping test).</summary>
public sealed class ShellTests : IAsyncLifetime
{
    private IdentityApiFactory? _factory;

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityApiFactory();
        await _factory.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    public async Task Outer_shell_endpoints_answer(string path)
    {
        using var client = _factory!.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
