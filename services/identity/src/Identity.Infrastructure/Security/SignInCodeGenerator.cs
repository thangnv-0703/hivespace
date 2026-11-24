using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Six-digit codes from <see cref="RandomNumberGenerator"/>; the stored hash is SHA-256 over the 16 bytes of the challenge id
/// (RFC 4122 order) followed by the UTF-8 code, compared in constant time (NFR-SEC-016, FEAT-006 D-7).
/// </summary>
public sealed class SignInCodeGenerator : ISignInCodeGenerator
{
    private const int Range = 1_000_000;

    private readonly Func<int, int, int> _nextInt;

    public SignInCodeGenerator()
        : this(RandomNumberGenerator.GetInt32)
    {
    }

    /// <param name="nextInt">The random source, <c>(fromInclusive, toExclusive) → value</c>; tests only.</param>
    internal SignInCodeGenerator(Func<int, int, int> nextInt) => _nextInt = nextInt;

    /// <summary>The random source in use; exposed so a test can show it is the CSPRNG.</summary>
    internal Func<int, int, int> RandomSource => _nextInt;

    public string NewCode() => _nextInt(0, Range).ToString("D6", CultureInfo.InvariantCulture);

    public byte[] HashCode(Guid challengeId, string code)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var input = new byte[16 + codeBytes.Length];
        challengeId.TryWriteBytes(input, bigEndian: true, out _);
        codeBytes.CopyTo(input, 16);
        return SHA256.HashData(input);
    }

    public bool Matches(Guid challengeId, string code, byte[] codeHash) =>
        CryptographicOperations.FixedTimeEquals(HashCode(challengeId, code), codeHash);

    public byte[] HashEmail(string normalizedEmail) => SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail));
}
