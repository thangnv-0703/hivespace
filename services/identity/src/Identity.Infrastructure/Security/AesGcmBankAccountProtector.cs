using System.Security.Cryptography;
using System.Text;
using Identity.Application.Abstractions;
using Identity.Domain;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

/// <summary>
/// AES-256-GCM with a random 12-byte nonce. Stored bytes are <c>0x01 | nonce | tag | ciphertext</c>; the shop id's 16 bytes are the
/// associated data (FEAT-010 D-1). The key id travels with the row, so a new active key never breaks old rows.
/// </summary>
internal sealed class AesGcmBankAccountProtector : IBankAccountProtector
{
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly string activeKeyId;
    private readonly Dictionary<string, byte[]> keys;

    public AesGcmBankAccountProtector(IOptions<BankEncryptionOptions> options)
    {
        var value = options.Value;
        activeKeyId = value.ActiveKeyId;
        keys = value.Keys.ToDictionary(k => k.Key, k => Convert.FromBase64String(k.Value));
    }

    public EncryptedValue Protect(BankAccountNumber number, Guid shopId)
    {
        var plaintext = Encoding.UTF8.GetBytes(number.Digits);
        var result = new byte[1 + NonceSize + TagSize + plaintext.Length];
        result[0] = FormatVersion;
        var nonce = result.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(keys[activeKeyId], TagSize);
        aes.Encrypt(nonce, plaintext, result.AsSpan(1 + NonceSize + TagSize), result.AsSpan(1 + NonceSize, TagSize), shopId.ToByteArray());
        CryptographicOperations.ZeroMemory(plaintext);
        return new EncryptedValue(activeKeyId, result);
    }

    public BankAccountNumber Unprotect(EncryptedValue value, Guid shopId)
    {
        var bytes = value.Bytes;
        if (bytes.Length < 1 + NonceSize + TagSize || bytes[0] != FormatVersion)
        {
            throw new CryptographicException("The stored bank account value has an unknown format.");
        }

        if (!keys.TryGetValue(value.KeyId, out var key))
        {
            throw new CryptographicException($"No bank encryption key '{value.KeyId}' is configured.");
        }

        var plaintext = new byte[bytes.Length - 1 - NonceSize - TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(bytes.AsSpan(1, NonceSize), bytes.AsSpan(1 + NonceSize + TagSize), bytes.AsSpan(1 + NonceSize, TagSize), plaintext, shopId.ToByteArray());
        var digits = Encoding.UTF8.GetString(plaintext);
        CryptographicOperations.ZeroMemory(plaintext);
        return BankAccountNumber.FromDecrypted(digits);
    }
}
