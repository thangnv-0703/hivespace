namespace Identity.Domain;

/// <summary>One unmet rule: the offending field and its stable error code (contracts/errors).</summary>
public sealed record Violation(string Field, string Code);
