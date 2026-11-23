using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace FakeIdp;

/// <summary>
/// Runs the fake IdP in-process on a real loopback port, for tests: Identity's OpenID Connect handler reaches it over HTTP like it
/// reaches Google. <see cref="StopAsync"/> and <see cref="StartAsync(FakeIdpServer)"/> simulate Google refusing connections and coming back.
/// </summary>
public sealed class FakeIdpServer : IAsyncDisposable
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string[] _redirectUris;
    private WebApplication? _app;

    private FakeIdpServer(string clientId, string clientSecret, string[] redirectUris, FakeIdpState state)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _redirectUris = redirectUris;
        State = state;
    }

    /// <summary>The issuer and discovery base, e.g. <c>http://127.0.0.1:54321</c>.</summary>
    public string Authority { get; private set; } = string.Empty;

    public FakeIdpState State { get; }

    public static async Task<FakeIdpServer> StartAsync(string clientId, string clientSecret, params string[] redirectUris)
    {
        var server = new FakeIdpServer(clientId, clientSecret, redirectUris, new FakeIdpState());
        await server.RunAsync("http://127.0.0.1:0").ConfigureAwait(false);
        return server;
    }

    /// <summary>Starts a stopped server again on the same port and with the same keys and state.</summary>
    public static Task StartAsync(FakeIdpServer stopped) => stopped.RunAsync(stopped.Authority);

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        State.Dispose();
    }

    private async Task RunAsync(string url)
    {
        var args = new List<string> { "--urls", url, $"--{FakeIdpOptions.Section}:ClientId", _clientId, $"--{FakeIdpOptions.Section}:ClientSecret", _clientSecret };
        for (var i = 0; i < _redirectUris.Length; i++)
        {
            args.AddRange([$"--{FakeIdpOptions.Section}:RedirectUris:{i}", _redirectUris[i]]);
        }

        var app = FakeIdpApp.Build([.. args], builder =>
        {
            builder.Services.AddSingleton(State);
            builder.Logging.ClearProviders();
        });
        await app.StartAsync().ConfigureAwait(false);
        _app = app;
        Authority = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
    }
}
