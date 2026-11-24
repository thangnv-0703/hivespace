namespace Identity.Domain;

/// <summary>The sign-in lockout numbers (FR-AUTH-011; the OQ-21 proposals): five failures within 15 minutes lock for 15 minutes.</summary>
public static class LockoutPolicy
{
    public const int Threshold = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
}
