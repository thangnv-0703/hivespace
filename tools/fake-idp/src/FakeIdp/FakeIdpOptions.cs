namespace FakeIdp;

/// <summary>Settings bound from <c>FakeIdp:*</c>: the one client it accepts and the redirect URIs registered for it.</summary>
public sealed class FakeIdpOptions
{
    public const string Section = "FakeIdp";

    public string ClientId { get; set; } = "hivespace-local";

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Exact redirect URIs, as registered at Google (D-8).</summary>
    public IList<string> RedirectUris { get; } = [];
}
