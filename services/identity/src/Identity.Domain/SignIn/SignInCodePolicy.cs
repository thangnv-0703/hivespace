namespace Identity.Domain;

/// <summary>
/// The email-code sign-in numbers (FR-AUTH-028, FR-AUTH-032, FR-AUTH-033; the OQ-35 proposals): six digits, valid
/// 5 minutes, one request per address per 60 seconds, five wrong codes end the challenge.
/// </summary>
public static class SignInCodePolicy
{
    public const int Digits = 6;

    public const int MaxWrongCodes = 5;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ResendWait = TimeSpan.FromSeconds(60);
}
