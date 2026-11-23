namespace FakeIdp;

/// <summary>Entry point. Internal and namespaced so test projects that also host Identity see only Identity's <c>Program</c>.</summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        var app = FakeIdpApp.Build(args);
        await app.RunAsync();
    }
}
