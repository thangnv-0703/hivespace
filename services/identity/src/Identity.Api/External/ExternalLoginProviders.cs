using System.Security.Claims;
using Identity.Application.Features.ExternalLogin;

namespace Identity.Api.External;

/// <summary>An external sign-in provider: its route name, its authentication scheme and how its claims map (FEAT-004 D-1).</summary>
public interface IExternalLoginProvider
{
    string Name { get; }

    string Scheme { get; }

    ExternalIdentity Map(ClaimsPrincipal principal);
}

/// <summary>Google: the ID token claims <c>sub</c>, <c>email</c>, <c>email_verified</c> and <c>name</c> (design §3.2).</summary>
public sealed class GoogleLoginProvider : IExternalLoginProvider
{
    public const string ProviderName = "google";

    public string Name => ProviderName;

    public string Scheme => ProviderName;

    public ExternalIdentity Map(ClaimsPrincipal principal) =>
        new(
            ProviderName,
            principal.FindFirstValue("sub"),
            principal.FindFirstValue("email"),
            string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            principal.FindFirstValue("name"));
}

/// <summary>
/// The providers Identity knows by name, and the ones configured in this deployment. A known but unconfigured provider answers
/// <c>identity.external_login_unavailable</c>; an unknown name is a 404 (design §3.1, §3.6).
/// </summary>
public sealed class ExternalLoginProviders(IEnumerable<IExternalLoginProvider> configured)
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal) { GoogleLoginProvider.ProviderName };
    private readonly Dictionary<string, IExternalLoginProvider> _configured = configured.ToDictionary(p => p.Name, StringComparer.Ordinal);

    public static bool IsKnown(string name) => Known.Contains(name);

    public IExternalLoginProvider? Find(string name) => _configured.GetValueOrDefault(name);
}
