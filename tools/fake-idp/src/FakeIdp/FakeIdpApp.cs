using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FakeIdp;

/// <summary>Body of <c>POST /__fake/next</c>; omitted fields keep their defaults.</summary>
public sealed record NextRequest(string? Outcome, string? Sub, string? Email, string? Name);

/// <summary>
/// A minimal OpenID Connect provider (FEAT-004 design §5.6): discovery, JWKS, the authorization code flow with PKCE S256, and a
/// control endpoint that chooses the next outcome. The issuer is the origin it is called on.
/// </summary>
public static class FakeIdpApp
{
    /// <summary>The cookie a browser test sets on the fake's origin to choose its own next sign-in.</summary>
    public const string NextCookie = "fake_next";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        configure?.Invoke(builder);
        builder.Services.Configure<FakeIdpOptions>(builder.Configuration.GetSection(FakeIdpOptions.Section));
        builder.Services.TryAddSingleton<FakeIdpState>();

        var app = builder.Build();
        Map(app);
        return app;
    }

    private static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok("ok"));

        app.MapGet("/.well-known/openid-configuration", (HttpContext http, FakeIdpState state) =>
        {
            if (state.IsDown)
            {
                return Unavailable();
            }

            var issuer = IssuerOf(http);
            return Results.Json(new Dictionary<string, object>
            {
                ["issuer"] = issuer,
                ["authorization_endpoint"] = issuer + "/authorize",
                ["token_endpoint"] = issuer + "/token",
                ["jwks_uri"] = issuer + "/jwks",
                ["response_types_supported"] = new[] { "code" },
                ["subject_types_supported"] = new[] { "public" },
                ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
                ["scopes_supported"] = new[] { "openid", "email", "profile" },
                ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post", "client_secret_basic" },
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["claims_supported"] = new[] { "sub", "email", "email_verified", "name", "iss", "aud", "exp", "iat", "nonce" },
            });
        });

        app.MapGet("/jwks", (FakeIdpState state) =>
        {
            if (state.IsDown)
            {
                return Unavailable();
            }

            var key = state.SigningKey.ExportParameters(false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new { kty = "RSA", use = "sig", alg = "RS256", kid = state.KeyId, n = B64(key.Modulus!), e = B64(key.Exponent!) },
                },
            });
        });

        app.MapGet("/authorize", Authorize);
        app.MapPost("/token", TokenAsync);

        app.MapPost("/__fake/next", (NextRequest body, FakeIdpState state) =>
        {
            var outcome = body.Outcome ?? Outcomes.Ok;
            if (!Outcomes.All.Contains(outcome))
            {
                return Results.BadRequest(new { error = $"unknown outcome '{outcome}'", outcomes = Outcomes.All });
            }

            var d = NextSignIn.Default;
            state.Next = new NextSignIn(outcome, body.Sub ?? d.Sub, body.Email ?? d.Email, body.Name ?? d.Name);
            return Results.Ok(state.Next);
        });

        app.MapGet("/__fake/last", (FakeIdpState state) => Results.Ok(state.Seen));
    }

    private static IResult Authorize(HttpContext http, FakeIdpState state, IOptions<FakeIdpOptions> options)
    {
        var query = http.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.Ordinal);
        state.Seen = state.Seen with { Authorize = query };
        var get = (string name) => query.GetValueOrDefault(name);

        // Like a real provider: a bad client or redirect URI is shown to the user, never redirected to.
        if (get("client_id") != options.Value.ClientId)
        {
            return Results.BadRequest(new { error = "invalid_client" });
        }

        var redirectUri = get("redirect_uri");
        if (redirectUri is null || !options.Value.RedirectUris.Contains(redirectUri, StringComparer.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri is not registered" });
        }

        var oauthState = get("state");
        var next = NextFromCookie(http) ?? state.Next;
        string? error = null;
        if (get("response_type") != "code" || get("code_challenge_method") != "S256" || string.IsNullOrEmpty(get("code_challenge")))
        {
            error = "invalid_request";
        }
        else if (next.Outcome == Outcomes.Denied)
        {
            error = "access_denied";
        }
        else if (next.Outcome == Outcomes.ServerError)
        {
            error = "server_error";
        }

        var back = new Dictionary<string, string?>();
        if (error is not null)
        {
            back["error"] = error;
        }
        else
        {
            back["code"] = state.Issue(new IssuedCode(get("client_id")!, redirectUri, get("code_challenge")!, get("nonce"), next));
        }

        if (oauthState is not null)
        {
            back["state"] = oauthState;
        }

        return Results.Redirect(Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(redirectUri, back));
    }

    private static async Task<IResult> TokenAsync(HttpContext http, FakeIdpState state, IOptions<FakeIdpOptions> options)
    {
        if (state.IsDown)
        {
            return Unavailable();
        }

        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var fields = form.ToDictionary(f => f.Key, f => f.Value.ToString(), StringComparer.Ordinal);
        var (clientId, secret) = ClientCredentials(http, fields);
        fields.Remove("client_secret");
        state.Seen = state.Seen with { Token = fields };

        if (clientId != options.Value.ClientId || secret is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(options.Value.ClientSecret)))
        {
            return Error(StatusCodes.Status401Unauthorized, "invalid_client");
        }

        if (fields.GetValueOrDefault("grant_type") != "authorization_code")
        {
            return Error(StatusCodes.Status400BadRequest, "unsupported_grant_type");
        }

        var code = state.Redeem(fields.GetValueOrDefault("code"));
        if (code is null || code.ClientId != clientId || code.RedirectUri != fields.GetValueOrDefault("redirect_uri")
            || !PkceMatches(fields.GetValueOrDefault("code_verifier"), code.CodeChallenge))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_grant");
        }

        var now = TimeProvider.System.GetUtcNow();
        var idToken = IdToken(state, code, IssuerOf(http), clientId, now);
        return Results.Json(new
        {
            access_token = B64(RandomNumberGenerator.GetBytes(32)),
            token_type = "Bearer",
            expires_in = 3600,
            scope = "openid email profile",
            id_token = idToken,
        });
    }

    private static string IdToken(FakeIdpState state, IssuedCode code, string issuer, string clientId, DateTimeOffset now)
    {
        var outcome = code.SignIn.Outcome;
        var issuedAt = outcome == Outcomes.Expired ? now.AddHours(-2) : now;
        var claims = new Dictionary<string, object>
        {
            ["iss"] = outcome == Outcomes.WrongIssuer ? "https://evil.example" : issuer,
            ["aud"] = outcome == Outcomes.WrongAudience ? "someone-else" : clientId,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = issuedAt.AddHours(1).ToUnixTimeSeconds(),
            ["name"] = code.SignIn.Name,
        };
        if (outcome != Outcomes.NoSub)
        {
            claims["sub"] = code.SignIn.Sub;
        }

        if (outcome != Outcomes.NoEmail)
        {
            claims["email"] = code.SignIn.Email;
            claims["email_verified"] = outcome != Outcomes.Unverified;
        }

        if (code.Nonce is not null)
        {
            claims["nonce"] = outcome == Outcomes.WrongNonce ? "not-the-nonce-" + now.Ticks.ToString(CultureInfo.InvariantCulture) : code.Nonce;
        }

        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = state.KeyId }, Json));
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(claims, Json));
        var input = Encoding.ASCII.GetBytes(header + "." + payload);
        var key = outcome == Outcomes.BadSignature ? state.OtherKey : state.SigningKey;
        var signature = key.SignData(input, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + payload + "." + B64(signature);
    }

    /// <summary>
    /// A per-browser override of <see cref="FakeIdpState.Next"/>: a <c>fake_next</c> cookie on the fake's own origin holding the
    /// same JSON as <c>POST /__fake/next</c>. Browser tests running in parallel set it per browser context, so they never see
    /// each other's identity.
    /// </summary>
    private static NextSignIn? NextFromCookie(HttpContext http)
    {
        if (!http.Request.Cookies.TryGetValue(NextCookie, out var raw) || string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var body = JsonSerializer.Deserialize<NextRequest>(raw, Json);
        var d = NextSignIn.Default;
        var outcome = body?.Outcome ?? Outcomes.Ok;
        return Outcomes.All.Contains(outcome) ? new NextSignIn(outcome, body?.Sub ?? d.Sub, body?.Email ?? d.Email, body?.Name ?? d.Name) : null;
    }

    private static (string? ClientId, string? Secret) ClientCredentials(HttpContext http, Dictionary<string, string> fields)
    {
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim()));
            var colon = decoded.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                return (Uri.UnescapeDataString(decoded[..colon]), Uri.UnescapeDataString(decoded[(colon + 1)..]));
            }
        }

        return (fields.GetValueOrDefault("client_id"), fields.GetValueOrDefault("client_secret"));
    }

    private static bool PkceMatches(string? verifier, string challenge) =>
        verifier is not null && B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) == challenge;

    private static string IssuerOf(HttpContext http) => $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}";

    private static JsonHttpResult<object> Unavailable() =>
        TypedResults.Json<object>(new { error = "temporarily_unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    private static JsonHttpResult<object> Error(int status, string error) => TypedResults.Json<object>(new { error }, statusCode: status);

    private static string B64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
