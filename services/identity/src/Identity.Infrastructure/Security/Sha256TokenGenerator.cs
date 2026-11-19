using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;

namespace Identity.Infrastructure.Security;

/// <summary>32 random bytes as base64url; only the SHA-256 of the token string is stored, never the token (R-6, AC-8).</summary>
public sealed class Sha256TokenGenerator : IVerificationTokenGenerator
{
    public GeneratedToken Generate()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new GeneratedToken(token, Hash(token));
    }

    public byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
