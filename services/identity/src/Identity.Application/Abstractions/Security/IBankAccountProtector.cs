using Identity.Domain;

namespace Identity.Application.Abstractions;

/// <summary>
/// Encrypts a shop's bank account number for storage (FEAT-010 D-1, NFR-SEC-008). The shop id is bound to the ciphertext, so bytes
/// copied to another shop do not decrypt. <see cref="Unprotect"/> is for round-trip tests and a future key rotation; no endpoint calls it.
/// </summary>
public interface IBankAccountProtector
{
    EncryptedValue Protect(BankAccountNumber number, Guid shopId);

    BankAccountNumber Unprotect(EncryptedValue value, Guid shopId);
}
