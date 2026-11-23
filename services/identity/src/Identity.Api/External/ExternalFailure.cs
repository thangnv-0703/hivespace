namespace Identity.Api.External;

/// <summary>The stable codes a failed external sign-in ends with, in <c>/signin?error=</c> (design §3.4, D-6).</summary>
public static class ExternalFailure
{
    public const string Denied = "identity.external_login_denied";
    public const string EmailUnverified = "identity.external_email_unverified";
    public const string Failed = "identity.external_login_failed";
    public const string Unavailable = "identity.external_login_unavailable";

    /// <summary>
    /// Maps a provider <c>error</c> (from its redirect or token response) and the handler's exception to one code. The provider being
    /// unreachable, slow or saying so is <see cref="Unavailable"/>; a consent refusal is <see cref="Denied"/>; anything else, including
    /// every validation failure (state, nonce, signature, issuer, audience, expiry, PKCE), is <see cref="Failed"/>.
    /// </summary>
    public static string Classify(string? error, Exception? exception)
    {
        switch (error)
        {
            case "access_denied":
                return Denied;
            case "temporarily_unavailable" or "server_error":
                return Unavailable;
            case not null:
                return Failed;
        }

        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException or TimeoutException or OperationCanceledException or IOException)
            {
                return Unavailable;
            }
        }

        return Failed;
    }

    /// <summary>The <c>error</c> the OpenID Connect handler copied from the provider's response into the exception, if any.</summary>
    public static string? ProviderErrorOf(Exception? exception) => exception?.Data["error"] as string;

    /// <summary><c>/signin?error=…[&amp;returnUrl=…]</c>; <paramref name="returnUrl"/> must already be safe or null.</summary>
    public static string SignInUrl(string code, string? returnUrl) =>
        returnUrl is null
            ? $"/signin?error={code}"
            : $"/signin?error={code}&returnUrl={Uri.EscapeDataString(returnUrl)}";
}
