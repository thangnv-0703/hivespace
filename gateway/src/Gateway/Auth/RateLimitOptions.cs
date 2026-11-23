namespace Gateway.Auth;

/// <summary>
/// The NFR-SEC-005 numbers, bound from <c>RateLimits:*</c>. The defaults are the requirement; only the E2E and system-test stacks
/// raise them, because they sign in many users from one loopback address (FEAT-005 D-9).
/// </summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
