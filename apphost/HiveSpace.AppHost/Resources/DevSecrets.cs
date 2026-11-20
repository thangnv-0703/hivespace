using System.Security.Cryptography;

namespace HiveSpace.AppHost.Resources;

/// <summary>
/// Local-only secrets for the identity server (FEAT-001 D-7): generated once and kept in the gitignored
/// <c>.local/dev-secrets</c> folder at the repo root, so tokens and sessions survive AppHost restarts.
/// Deployed environments get their own values from a vault (FEAT-037); nothing here is a real secret.
/// </summary>
internal sealed record IdentitySecrets(string SigningKeyPem, string EncryptionKeyPem, string GatewayClientSecret);

internal static class DevSecrets
{
    public static IdentitySecrets LoadIdentitySecrets(string appHostDirectory)
    {
        var folder = Path.GetFullPath(Path.Combine(appHostDirectory, "..", "..", ".local", "dev-secrets"));
        Directory.CreateDirectory(folder);
        return new IdentitySecrets(
            GetOrCreate(folder, "identity-signing-key.pem", () => ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem()),
            GetOrCreate(folder, "identity-encryption-key.pem", () => RSA.Create(2048).ExportPkcs8PrivateKeyPem()),
            GetOrCreate(folder, "gateway-client-secret.txt", () => Convert.ToHexString(RandomNumberGenerator.GetBytes(32))));
    }

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
