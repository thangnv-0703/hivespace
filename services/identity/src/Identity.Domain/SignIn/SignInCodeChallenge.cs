namespace Identity.Domain;

/// <summary>Why a sign-in code challenge stopped accepting codes.</summary>
public enum ChallengeEndReason
{
    Used,
    Superseded,
    Attempts,
}

/// <summary>
/// One emailed sign-in code (FR-AUTH-028). Only hashes are stored: the address (so the 60-second rule and "a new code
/// ends the earlier ones" work for any address, FR-AUTH-029, FR-AUTH-033, FR-AUTH-034) and the code salted with the
/// challenge id (NFR-SEC-016). <see cref="UserId"/> is null for an address with no account (design D-1).
/// </summary>
public sealed class SignInCodeChallenge
{
    private SignInCodeChallenge()
    {
        EmailHash = [];
        CodeHash = [];
    }

    public Guid Id { get; private set; }

    public byte[] EmailHash { get; private set; }

    public Guid? UserId { get; private set; }

    public byte[] CodeHash { get; private set; }

    public int FailedCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public ChallengeEndReason? EndReason { get; private set; }

    public bool IsEnded => EndedAt is not null;

    public DateTimeOffset ResendAvailableAt => CreatedAt + SignInCodePolicy.ResendWait;

    /// <summary>Issues an open challenge. The id is passed in because the code hash is salted with it.</summary>
    public static SignInCodeChallenge Issue(Guid id, byte[] emailHash, Guid? userId, byte[] codeHash, DateTimeOffset now) =>
        new()
        {
            Id = id,
            EmailHash = emailHash,
            UserId = userId,
            CodeHash = codeHash,
            CreatedAt = now,
            ExpiresAt = now + SignInCodePolicy.Lifetime,
        };

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>The wait left before a new code may be requested for this address, or zero.</summary>
    public TimeSpan RetryAfter(DateTimeOffset now) =>
        ResendAvailableAt > now ? ResendAvailableAt - now : TimeSpan.Zero;

    public void Supersede(DateTimeOffset now) => End(ChallengeEndReason.Superseded, now);

    public void MarkUsed(DateTimeOffset now) => End(ChallengeEndReason.Used, now);

    /// <summary>Counts one wrong code; the fifth ends the challenge (FR-AUTH-032). Returns true when this call ended it.</summary>
    public bool RecordWrongCode(DateTimeOffset now)
    {
        if (IsEnded)
        {
            return false;
        }

        FailedCount++;
        if (FailedCount < SignInCodePolicy.MaxWrongCodes)
        {
            return false;
        }

        End(ChallengeEndReason.Attempts, now);
        return true;
    }

    private void End(ChallengeEndReason reason, DateTimeOffset now)
    {
        if (IsEnded)
        {
            return;
        }

        EndedAt = now;
        EndReason = reason;
    }
}
