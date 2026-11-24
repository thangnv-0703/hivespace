using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.CodeSignIn;

public sealed record RequestSignInCodeCommand(string? Email);

/// <summary>Payload of <c>SignInCodeRequested/v1</c> (contracts/events). The code is never logged (R-6, NFR-SEC-016).</summary>
public sealed record SignInCodeRequestedData(
    Guid UserId,
    string Email,
    string DisplayName,
    string Language,
    string Code,
    DateTimeOffset ExpiresAt);

/// <summary>The answer to a code request: a challenge was issued, or the address asked less than 60 seconds ago.</summary>
public abstract record SignInCodeRequestOutcome
{
    private SignInCodeRequestOutcome()
    {
    }

    public sealed record Issued(Guid ChallengeId, DateTimeOffset ExpiresAt, DateTimeOffset ResendAvailableAt) : SignInCodeRequestOutcome;

    public sealed record TooSoon(int RetryAfterSeconds) : SignInCodeRequestOutcome;
}

/// <summary>
/// FR-AUTH-028, FR-AUTH-029, FR-AUTH-033, FR-AUTH-034 (design §3.2): a malformed email is rejected; any other address gets a
/// challenge, ending its open ones, at most once per 60 seconds. The code is emailed through <c>SignInCodeRequested</c> only when an
/// account exists; an unknown address does the same work without the event (D-1). A locked account still gets a code (D-4).
/// </summary>
public sealed class RequestSignInCodeHandler(
    IUserRepository users,
    ISignInChallengeStore challenges,
    ISignInCodeGenerator codes,
    IEventOutbox outbox,
    TimeProvider clock)
{
    public const string SignInCodeRequestedEvent = "SignInCodeRequested";

    public async Task<Result<SignInCodeRequestOutcome>> HandleAsync(RequestSignInCodeCommand command, CancellationToken cancellationToken)
    {
        var (email, violation) = Email.Create(command.Email);
        if (violation is not null)
        {
            return Result<SignInCodeRequestOutcome>.Fail(ErrorCodes.ValidationFailed, [violation]);
        }

        var now = clock.GetUtcNow();
        var emailHash = codes.HashEmail(email!.Normalized);
        if (await challenges.FindNewestAsync(emailHash, cancellationToken).ConfigureAwait(false) is { } newest
            && newest.RetryAfter(now) > TimeSpan.Zero)
        {
            return Result<SignInCodeRequestOutcome>.Ok(new SignInCodeRequestOutcome.TooSoon(Seconds(newest.RetryAfter(now))));
        }

        var user = await users.FindByEmailAsync(email.Normalized, cancellationToken).ConfigureAwait(false);
        var openChallenges = await challenges.FindOpenAsync(emailHash, cancellationToken).ConfigureAwait(false);
        // A parallel request may have committed between the two reads: the 60-second rule applies to it too.
        var wait = openChallenges.Count == 0 ? TimeSpan.Zero : openChallenges.Max(c => c.RetryAfter(now));
        if (wait > TimeSpan.Zero)
        {
            return Result<SignInCodeRequestOutcome>.Ok(new SignInCodeRequestOutcome.TooSoon(Seconds(wait)));
        }

        foreach (var open in openChallenges)
        {
            open.Supersede(now);
        }

        var id = Guid.CreateVersion7(now);
        // Always generated and hashed, so an unknown address costs the same work (FR-AUTH-029).
        var code = codes.NewCode();
        var challenge = SignInCodeChallenge.Issue(id, emailHash, user?.Id, codes.HashCode(id, code), now);
        challenges.Add(challenge);
        if (user is not null)
        {
            outbox.Enqueue(
                SignInCodeRequestedEvent,
                1,
                user.Id.ToString(),
                new SignInCodeRequestedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, code, challenge.ExpiresAt));
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SignInChallengeConflictException)
        {
            // A parallel request for the address committed first (design §3.2).
            return Result<SignInCodeRequestOutcome>.Ok(new SignInCodeRequestOutcome.TooSoon(Seconds(SignInCodePolicy.ResendWait)));
        }

        return Result<SignInCodeRequestOutcome>.Ok(
            new SignInCodeRequestOutcome.Issued(challenge.Id, challenge.ExpiresAt, challenge.ResendAvailableAt));
    }

    private static int Seconds(TimeSpan wait) => (int)Math.Ceiling(wait.TotalSeconds);
}
