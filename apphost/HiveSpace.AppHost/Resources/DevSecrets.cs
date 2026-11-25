using System.Security.Cryptography;

namespace HiveSpace.AppHost.Resources;

/// <summary>
/// Local-only secrets for the identity server (FEAT-001 D-7): generated once and kept in the gitignored
/// <c>.local/dev-secrets</c> folder at the repo root, so tokens and sessions survive AppHost restarts.
/// Deployed environments get their own values from a vault (FEAT-037); nothing here is a real secret.
/// </summary>
internal sealed record IdentitySecrets(string SigningKeyPem, string EncryptionKeyPem, string StorefrontClientSecret, string SellerClientSecret, string BankEncryptionKeyV1);

/// <summary>
/// The Google client Identity uses (FEAT-004 §3.6). <see cref="Real"/> is false when the fake IdP stands in for Google;
/// then <see cref="Authority"/> is empty and the AppHost points Identity at the fake.
/// </summary>
internal sealed record GoogleClient(bool Real, string ClientId, string ClientSecret, string Authority);

internal static class DevSecrets
{
    public static IdentitySecrets LoadIdentitySecrets(string appHostDirectory)
    {
        var folder = Path.GetFullPath(Path.Combine(appHostDirectory, "..", "..", ".local", "dev-secrets"));
        Directory.CreateDirectory(folder);
        return new IdentitySecrets(
            GetOrCreate(folder, "identity-signing-key.pem", () => ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem()),
            GetOrCreate(folder, "identity-encryption-key.pem", () => RSA.Create(2048).ExportPkcs8PrivateKeyPem()),
            GetOrCreate(folder, "storefront-client-secret.txt", NewClientSecret),
            GetOrCreate(folder, "seller-client-secret.txt", NewClientSecret),
            // 32 random bytes in base64 for AES-256-GCM (FEAT-010 D-1); deployed keys come from a vault (FEAT-037).
            GetOrCreate(folder, "bank-encryption-key-v1.txt", () => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    /// <summary>
    /// A real Google test client from <c>.local/dev-secrets/google.json</c> (<c>{"clientId", "clientSecret"}</c>, OQ-32), or else
    /// the fake IdP's client with a generated secret (FEAT-004 D-7).
    /// </summary>
    public static GoogleClient LoadGoogleClient(string appHostDirectory)
    {
        var folder = Path.GetFullPath(Path.Combine(appHostDirectory, "..", "..", ".local", "dev-secrets"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "google.json");
        if (File.Exists(file))
        {
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            var root = json.RootElement;
            return new GoogleClient(
                true,
                root.GetProperty("clientId").GetString()!,
                root.GetProperty("clientSecret").GetString()!,
                root.TryGetProperty("authority", out var authority) ? authority.GetString()! : "https://accounts.google.com");
        }

        return new GoogleClient(false, "hivespace-local", GetOrCreate(folder, "fake-google-client-secret.txt", NewClientSecret), string.Empty);
    }

    private static string NewClientSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string GetOrCreate(string folder, string name, Func<string> generate)
    {
        var path = Path.Combine(folder, name);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        var value = generate();
        File.WriteAllText(path, value);
        return value;
    }
}
