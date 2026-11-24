namespace Identity.Application.Abstractions;

/// <summary>A freshly generated token: the value that goes in the email and the hash that is stored (R-6).</summary>
public sealed record GeneratedToken(string Token, byte[] Hash);
