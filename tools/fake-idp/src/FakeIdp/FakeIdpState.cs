using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace FakeIdp;

/// <summary>What the next sign-in returns (design §5.6). Every outcome but <see cref="Ok"/> makes something fail on purpose.</summary>
public static class Outcomes
{
    public const string Ok = "ok";
    public const string Denied = "denied";
    public const string ServerError = "server_error";
    public const string Unverified = "unverified";
    public const string NoEmail = "no_email";
    public const string NoSub = "no_sub";
    public const string BadSignature = "bad_signature";
    public const string WrongAudience = "wrong_audience";
    public const string WrongIssuer = "wrong_issuer";
    public const string Expired = "expired";
    public const string WrongNonce = "wrong_nonce";
    public const string Down = "down";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Ok, Denied, ServerError, Unverified, NoEmail, NoSub, BadSignature, WrongAudience, WrongIssuer, Expired, WrongNonce, Down,
    };
}

/// <summary>The identity and outcome set by <c>POST /__fake/next</c>; it stays until the next call.</summary>
public sealed record NextSignIn(string Outcome, string Sub, string Email, string Name)
{
    public static readonly NextSignIn Default = new(Outcomes.Ok, "fake-google-sub-1", "an.nguyen@gmail.com", "An Nguyen");
}

/// <summary>A code issued by <c>/authorize</c>, redeemable once at <c>/token</c>.</summary>
public sealed record IssuedCode(string ClientId, string RedirectUri, string CodeChallenge, string? Nonce, NextSignIn SignIn);

/// <summary>The last requests seen, so tests can assert the exact parameters Identity sent (AC-7). The client secret is never kept.</summary>
public sealed record SeenRequests(IReadOnlyDictionary<string, string>? Authorize, IReadOnlyDictionary<string, string>? Token);

public sealed class FakeIdpState : IDisposable
{
    private readonly ConcurrentDictionary<string, IssuedCode> _codes = new(StringComparer.Ordinal);
    private volatile NextSignIn _next = NextSignIn.Default;
    private volatile SeenRequests _seen = new(null, null);

    public RSA SigningKey { get; } = RSA.Create(2048);

    /// <summary>A key that is not in the JWKS but uses the same <c>kid</c>, for <see cref="Outcomes.BadSignature"/>.</summary>
    public RSA OtherKey { get; } = RSA.Create(2048);

    public string KeyId { get; } = "fake-idp-key-1";

    public NextSignIn Next
    {
        get => _next;
        set => _next = value;
    }

    public SeenRequests Seen
    {
        get => _seen;
        set => _seen = value;
    }

    public bool IsDown => _next.Outcome == Outcomes.Down;

    public string Issue(IssuedCode code)
    {
        var value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _codes[value] = code;
        return value;
    }

    public IssuedCode? Redeem(string? code) => code is not null && _codes.TryRemove(code, out var issued) ? issued : null;

    public void Dispose()
    {
        SigningKey.Dispose();
        OtherKey.Dispose();
    }
}
