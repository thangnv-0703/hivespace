using System.Net;
using FakeIdp;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Identity.IntegrationTests;

/// <summary>
/// Plays the browser through Google sign-in: Identity (in-memory test server, with cookies) and the fake IdP (real HTTP). The
/// callback URL Google is given is the public one (<see cref="IdentityApiFactory.GoogleCallback"/>); like the gateway, the browser
/// reaches it on Identity.
/// </summary>
public sealed class GoogleBrowser : IDisposable
{
    private readonly HttpClient _idp = new(new HttpClientHandler { AllowAutoRedirect = false });

    public GoogleBrowser(IdentityApiFactory factory, FakeIdpServer idp)
    {
        Identity = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        Idp = idp;
    }

    public HttpClient Identity { get; }

    public FakeIdpServer Idp { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>One request and what came back.</summary>
    public sealed record Step(string Url, HttpStatusCode Status, string? Location, IReadOnlyList<string> SetCookies);

    /// <summary>The steps of one run, start to the last redirect Identity gave.</summary>
    public sealed record Run(IReadOnlyList<Step> Steps)
    {
        public string? FinalLocation => Steps[^1].Location;

        public IEnumerable<string> SetCookies => Steps.SelectMany(s => s.SetCookies);

        /// <summary>The query of the redirect to Google, if the run got that far.</summary>
        public Step? ToGoogle => Steps.FirstOrDefault(s => s.Url.StartsWith("/connect/external/google/start", StringComparison.Ordinal));
    }

    public static string StartUrl(string? returnUrl, string provider = "google") =>
        returnUrl is null
            ? $"/connect/external/{provider}/start"
            : $"/connect/external/{provider}/start?returnUrl={Uri.EscapeDataString(returnUrl)}";

    /// <summary>Start → Google authorize → callback → complete, stopping at the first redirect that leaves the flow.</summary>
    public async Task<Run> SignInAsync(string? returnUrl, Func<string, string>? tamperCallback = null)
    {
        var steps = new List<Step>();
        var start = await GetAsync(Identity, StartUrl(returnUrl));
        steps.Add(start);
        if (start.Location is null || !start.Location.StartsWith(Idp.Authority, StringComparison.Ordinal))
        {
            return new Run(steps);
        }

        var authorize = await GetAsync(_idp, start.Location);
        steps.Add(authorize);
        var callback = authorize.Location;
        if (callback is null || !callback.StartsWith(IdentityApiFactory.GoogleCallback, StringComparison.Ordinal))
        {
            return new Run(steps);
        }

        callback = callback[IdentityApiFactory.Issuer.Length..];
        steps.Add(await GetAsync(Identity, tamperCallback is null ? callback : tamperCallback(callback)));
        return await ContinueAsync(steps);
    }

    /// <summary>Follows Identity's redirects inside <c>/connect/external/</c> (the complete step).</summary>
    public async Task<Run> ContinueAsync(List<Step> steps)
    {
        while (steps[^1].Location is { } next && next.StartsWith("/connect/external/", StringComparison.Ordinal))
        {
            steps.Add(await GetAsync(Identity, next));
        }

        return new Run(steps);
    }

    /// <summary>Only the first leg: start, then Google's redirect back, without calling the callback.</summary>
    public async Task<string> CallbackUrlAsync(string? returnUrl)
    {
        var start = await GetAsync(Identity, StartUrl(returnUrl));
        var authorize = await GetAsync(_idp, start.Location!);
        return authorize.Location![IdentityApiFactory.Issuer.Length..];
    }

    public async Task<Step> GetAsync(string url) => await GetAsync(Identity, url);

    public void Dispose()
    {
        Identity.Dispose();
        _idp.Dispose();
    }

    private static async Task<Step> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute), Ct);
        return new Step(
            url,
            response.StatusCode,
            response.Headers.Location?.OriginalString,
            response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : []);
    }
}
