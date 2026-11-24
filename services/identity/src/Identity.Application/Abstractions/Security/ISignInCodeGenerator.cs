namespace Identity.Application.Abstractions;

/// <summary>Six-digit sign-in codes and the hashes stored for them (NFR-SEC-016, FEAT-006 D-7).</summary>
public interface ISignInCodeGenerator
{
    /// <summary>Six digits from a cryptographically secure generator.</summary>
    string NewCode();

    /// <summary>The stored hash of a code, salted with its challenge id.</summary>
    byte[] HashCode(Guid challengeId, string code);

    /// <summary>Compares a presented code with the stored hash in constant time.</summary>
    bool Matches(Guid challengeId, string code, byte[] codeHash);

    /// <summary>The stored hash of a normalized email; the plain address is never stored for a challenge (D-3).</summary>
    byte[] HashEmail(string normalizedEmail);
}
