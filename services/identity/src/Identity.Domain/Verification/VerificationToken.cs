namespace Identity.Domain;

/// <summary>The outcome of checking a presented token (FR-AUTH-005, FR-AUTH-006).</summary>
public enum TokenCheck
{
    Valid,
    Expired,
    AlreadyUsed,
}

/// <summary>
/// A single-use email verification token. Only its SHA-256 hash is stored (R-6). A token invalidated by a resend
/// reads as expired so the UI offers a new link.
/// </summary>
public sealed class VerificationToken
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private VerificationToken()
    {
        TokenHash = [];
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public byte[] TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset? InvalidatedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static VerificationToken Issue(Guid userId, byte[] tokenHash, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now + Lifetime,
            CreatedAt = now,
        };

    public TokenCheck Check(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            return TokenCheck.AlreadyUsed;
        }

        if (InvalidatedAt is not null || now >= ExpiresAt)
        {
            return TokenCheck.Expired;
        }

        return TokenCheck.Valid;
    }

    public void MarkUsed(DateTimeOffset now) => UsedAt ??= now;

    public void Invalidate(DateTimeOffset now)
    {
        if (UsedAt is null)
        {
            InvalidatedAt ??= now;
        }
    }
}
