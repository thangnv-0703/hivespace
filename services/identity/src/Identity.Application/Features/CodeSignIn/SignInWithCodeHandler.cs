using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Application.Features.Lockout;
using Identity.Application.Features.Registration;
using Identity.Domain;

namespace Identity.Application.Features.CodeSignIn;

public sealed record SignInWithCodeCommand(string? ChallengeId, string? Code, string? ReturnUrl);

/// <summary>
/// FR-AUTH-030..032, FR-AUTH-035, FR-AUTH-036 (design §3.3): checks a code against its challenge. The code is compared only for an
/// open, unexpired challenge of an account that is not locked. A wrong code counts on the challenge (five end it) and on the account's
/// lockout, in one save with the audit rows (D-5); a correct one ends the challenge, verifies the email and clears the lockout. An
/// unknown address's challenge answers exactly like a real one (D-1). Every submission is audited with method <c>code</c> (NFR-SEC-009).
/// </summary>
public sealed class SignInWithCodeHandler(
    IUserRepository users,
    ISignInChallengeStore challenges,
    ISignInCodeGenerator codes,
    ILockoutStore lockouts,
    IAuditLog audit,
    LockoutRecorder recorder,
    TimeProvider clock)
{
    public const string CodeInvalid = "identity.sign_in_code_invalid";
    public const string CodeExpired = "identity.sign_in_code_expired";
    public const string ChallengeEnded = "identity.sign_in_challenge_ended";

    // Each conflict means another failure or a success committed; both counters are bounded (design §5.2).
    private const int Attempts = LockoutPolicy.Threshold + SignInCodePolicy.MaxWrongCodes + 1;

    public async Task<Result<SignInOutcome>> HandleAsync(SignInWithCodeCommand command, CancellationToken cancellationToken)
    {
        var code = command.Code?.Trim();
        if (code is not { Length: SignInCodePolicy.Digits } || !code.All(char.IsAsciiDigit))
        {
            return Result<SignInOutcome>.Fail(ErrorCodes.ValidationFailed, [new Violation("code", CodeInvalid)]);
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await AttemptAsync(command, code, cancellationToken).ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException) when (attempt < Attempts)
            {
                // A parallel submission or failure committed first and nothing stayed staged: read again.
            }
        }
    }

    private async Task<Result<SignInOutcome>> AttemptAsync(SignInWithCodeCommand command, string code, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = Guid.TryParse(command.ChallengeId, out var id)
            ? await challenges.FindAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        if (challenge is null || challenge.IsEnded)
        {
            return await FailAsync(challenge?.UserId, ChallengeEnded, cancellationToken).ConfigureAwait(false);
        }

        if (challenge.IsExpired(now))
        {
            return await FailAsync(challenge.UserId, CodeExpired, cancellationToken).ConfigureAwait(false);
        }

        if (challenge.UserId is { } lockedUserId
            && await lockouts.FindAsync(lockedUserId, cancellationToken).ConfigureAwait(false) is { } lockout
            && lockout.IsLocked(now))
        {
            // FR-AUTH-035: the code is not even compared.
            return await FailAsync(lockedUserId, SignInHandler.AccountLocked, cancellationToken).ConfigureAwait(false);
        }

        // A challenge without an account was never emailed: even a guessed code is wrong (D-1).
        var matches = codes.Matches(challenge.Id, code, challenge.CodeHash) && challenge.UserId is not null;
        if (!matches)
        {
            var ended = challenge.RecordWrongCode(now) || challenge.IsEnded;
            var reason = ended ? ChallengeEnded : CodeInvalid;
            var locked = false;
            if (challenge.UserId is { } userId)
            {
                locked = await recorder.StageFailureAsync(userId, AuditMethod.Code, reason, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                audit.Add(AuditEntry.SignInFailed(null, AuditMethod.Code, reason, now));
            }

            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<SignInOutcome>.Fail(locked ? SignInHandler.AccountLocked : reason);
        }

        var user = await users.FindByIdAsync(challenge.UserId!.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The challenge's account does not exist.");
        challenge.MarkUsed(now);
        if (!user.IsEmailVerified)
        {
            // FR-AUTH-036: the code proves the mailbox.
            user.VerifyEmail(now);
        }

        await recorder.StageSuccessAsync(user.Id, AuditMethod.Code, cancellationToken).ConfigureAwait(false);
        var credential = await users.FindCredentialAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<SignInOutcome>.Ok(new SignInOutcome(user, SignInHandler.SafeRedirect(command.ReturnUrl), credential?.Stamp, now));
    }

    private async Task<Result<SignInOutcome>> FailAsync(Guid? userId, string reason, CancellationToken cancellationToken)
    {
        audit.Add(AuditEntry.SignInFailed(userId, AuditMethod.Code, reason, clock.GetUtcNow()));
        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<SignInOutcome>.Fail(reason);
    }
}
