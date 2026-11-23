namespace Identity.Domain;

/// <summary>How the actor proved who they are in an audited attempt.</summary>
public enum AuditMethod
{
    Password,
    Google,
    PasswordChange,
}

/// <summary>
/// One line of the security audit log (NFR-SEC-009). It carries no email, IP address or user agent (design D-4, NFR-LEG-001):
/// the user id when known, the method, an error code as the reason and a correlation id set by Infrastructure.
/// </summary>
public sealed class AuditEntry
{
    public const string SignInSucceededType = "sign_in_succeeded";
    public const string SignInFailedType = "sign_in_failed";
    public const string AccountLockedType = "account_locked";

    private AuditEntry()
    {
        Type = string.Empty;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string Type { get; private set; }

    public AuditMethod Method { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Reason { get; private set; }

    public string? CorrelationId { get; private set; }

    public static AuditEntry SignInSucceeded(Guid userId, AuditMethod method, DateTimeOffset now) =>
        Create(SignInSucceededType, userId, method, null, now);

    public static AuditEntry SignInFailed(Guid? userId, AuditMethod method, string reason, DateTimeOffset now) =>
        Create(SignInFailedType, userId, method, reason, now);

    public static AuditEntry AccountLocked(Guid userId, AuditMethod method, DateTimeOffset now) =>
        Create(AccountLockedType, userId, method, null, now);

    /// <summary>Set by Infrastructure from the current trace/request, never by the domain.</summary>
    public void SetCorrelationId(string? correlationId) => CorrelationId = correlationId;

    private static AuditEntry Create(string type, Guid? userId, AuditMethod method, string? reason, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OccurredAt = now,
            Type = type,
            Method = method,
            UserId = userId,
            Reason = reason,
        };
}
