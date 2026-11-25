using System.Text.RegularExpressions;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>Bank name, 2 to 80 characters (FR-SHOP-007).</summary>
public sealed record BankName
{
    private BankName(string value) => Value = value;

    public string Value { get; }

    public static (BankName? Name, Violation? Violation) Create(string? raw)
    {
        var (value, violation) = AddressText.Create(
            raw, "bankName", 2, 80, "identity.bank_name_required", "identity.bank_name_too_long", "identity.bank_name_invalid");
        return (value is null ? null : new BankName(value), violation);
    }

    /// <summary>Wraps a value read back from the database.</summary>
    public static BankName FromStored(string value) => new(value);
}

/// <summary>Account holder name, 2 to 80 characters (FR-SHOP-007).</summary>
public sealed record AccountHolder
{
    private AccountHolder(string value) => Value = value;

    public string Value { get; }

    public static (AccountHolder? Holder, Violation? Violation) Create(string? raw)
    {
        var (value, violation) = AddressText.Create(
            raw, "accountHolder", 2, 80, "identity.bank_account_holder_required", "identity.bank_account_holder_too_long", "identity.bank_account_holder_invalid");
        return (value is null ? null : new AccountHolder(value), violation);
    }

    /// <summary>Wraps a value read back from the database.</summary>
    public static AccountHolder FromStored(string value) => new(value);
}

/// <summary>
/// A bank account number: 6 to 19 ASCII digits once spaces and hyphens are removed (FEAT-010 D-7). It is a sealed class, not a record,
/// so a stray log line or string interpolation prints <see cref="ToString"/> (the masked form), never the digits (NFR-SEC-008).
/// </summary>
public sealed partial class BankAccountNumber
{
    private BankAccountNumber(string digits) => Digits = digits;

    /// <summary>The digits. Read only by the protector that encrypts them.</summary>
    public string Digits { get; }

    public string Last4 => Digits[^4..];

    public static (BankAccountNumber? Number, Violation? Violation) Create(string? raw)
    {
        var compact = (raw ?? string.Empty).Trim();
        if (compact.Length == 0)
        {
            return (null, new Violation("accountNumber", "identity.bank_account_number_required"));
        }

        compact = Separators().Replace(compact, string.Empty);
        return Shape().IsMatch(compact)
            ? (new BankAccountNumber(compact), null)
            : (null, new Violation("accountNumber", "identity.bank_account_number_invalid"));
    }

    /// <summary>Wraps digits decrypted from storage.</summary>
    public static BankAccountNumber FromDecrypted(string digits) => new(digits);

    public override string ToString() => "••••" + Last4;

    [GeneratedRegex(@"[ \-]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^[0-9]{6,19}$")]
    private static partial Regex Shape();
}

/// <summary>An encrypted value and the id of the key that sealed it, so old rows stay readable after a key rotation (FEAT-010 D-1).</summary>
public sealed record EncryptedValue(string KeyId, byte[] Bytes);

/// <summary>The shop's payout bank details. The number exists only as ciphertext plus its last four digits (FR-SHOP-010, NFR-SEC-008).</summary>
public sealed record PayoutDetails(BankName BankName, AccountHolder AccountHolder, EncryptedValue AccountNumber, string Last4);
