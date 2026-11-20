using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Konscious.Security.Cryptography;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Argon2id (D-3, NFR-SEC-002) with the OWASP minimum parameters: 19 MiB, 2 iterations, 1 lane. Output is a PHC string
/// <c>$argon2id$v=19$m=19456,t=2,p=1$salt$hash</c>, so parameters can change later without breaking old hashes.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemoryKiB = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private readonly Lazy<string> _dummy;

    public Argon2idPasswordHasher() => _dummy = new Lazy<string>(() => Hash(Guid.NewGuid().ToString("N")));

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, MemoryKiB, Iterations, Parallelism, HashBytes);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}${B64(salt)}${B64(hash)}");
    }

    public bool Verify(string? hash, string password)
    {
        var stored = hash ?? _dummy.Value;
        var parsed = Parse(stored);
        if (parsed is null)
        {
            return false;
        }

        var (memory, iterations, parallelism, salt, expected) = parsed.Value;
        var actual = Compute(password, salt, memory, iterations, parallelism, expected.Length);
        var equal = CryptographicOperations.FixedTimeEquals(actual, expected);
        return hash is not null && equal;
    }

    private static byte[] Compute(string password, byte[] salt, int memory, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memory,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(length);
    }

    private static (int Memory, int Iterations, int Parallelism, byte[] Salt, byte[] Hash)? Parse(string phc)
    {
        var parts = phc.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "argon2id")
        {
            return null;
        }

        int memory = 0, iterations = 0, parallelism = 0;
        foreach (var pair in parts[2].Split(','))
        {
            var kv = pair.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            switch (kv[0])
            {
                case "m": memory = number; break;
                case "t": iterations = number; break;
                case "p": parallelism = number; break;
            }
        }

        return (memory, iterations, parallelism, FromB64(parts[3]), FromB64(parts[4]));
    }

    // PHC uses unpadded standard base64.
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] FromB64(string text) =>
        Convert.FromBase64String(text.PadRight(text.Length + ((4 - (text.Length % 4)) % 4), '='));
}
