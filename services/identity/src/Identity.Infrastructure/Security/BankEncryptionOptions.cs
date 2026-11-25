namespace Identity.Infrastructure.Security;

/// <summary>
/// Keys for bank account numbers at rest, bound from <c>Identity:BankEncryption</c> (FEAT-010 D-1). <see cref="Keys"/> maps a key id to
/// 32 random bytes in base64; <see cref="ActiveKeyId"/> seals new values, and every listed key can still open old rows.
/// </summary>
public sealed class BankEncryptionOptions
{
    public const string SectionName = "Identity:BankEncryption";
    public const int KeyLength = 32;

    public string ActiveKeyId { get; set; } = string.Empty;

    public Dictionary<string, string> Keys { get; set; } = [];

    /// <summary>Why the options cannot be used, or null when they are valid.</summary>
    public string? Problem()
    {
        if (!Keys.TryGetValue(ActiveKeyId, out _))
        {
            return $"{SectionName}:ActiveKeyId '{ActiveKeyId}' has no entry in {SectionName}:Keys.";
        }

        var buffer = new byte[KeyLength + 4];
        foreach (var (id, value) in Keys)
        {
            if (id.Length is 0 or > 32)
            {
                return $"{SectionName}:Keys has a key id that is empty or longer than 32 characters.";
            }

            if (!Convert.TryFromBase64String(value, buffer, out var written) || written != KeyLength)
            {
                return $"{SectionName}:Keys:{id} must be base64 of exactly {KeyLength} bytes.";
            }
        }

        return null;
    }
}
