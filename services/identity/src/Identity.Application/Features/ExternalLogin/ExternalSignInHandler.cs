using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.ExternalLogin;

/// <summary>What the external provider proved about the visitor, already read from its validated ID token (D-2).</summary>
public sealed record ExternalIdentity(string Provider, string? Subject, string? Email, bool EmailVerified, string? Name);

public sealed record ExternalSignInCommand(ExternalIdentity Identity, string Language);

/// <summary>The user who signs in, and the password stamp for <c>hs.idp</c>, or null when the account has no password.</summary>
public sealed record ExternalSignInOutcome(User User, long? PasswordStamp);

/// <summary>Payload of <c>ExternalLoginLinked/v1</c> (contracts/events). No token, subject or password.</summary>
public sealed record ExternalLoginLinkedData(
    Guid UserId,
    string Email,
    string DisplayName,
    string Language,
    string Provider,
    bool AccountCreated,
    bool PasswordRemoved,
    DateTimeOffset LinkedAt);

/// <summary>
/// The account decision of Google sign-in (FEAT-004 design §5.2, FR-AUTH-020, FR-AUTH-021): a known <c>(provider, subject)</c> signs
/// in (D-4), a verified email links to its account or creates a verified Buyer. Linking an account whose email was never verified
/// removes its password and sessions (D-5). Everything is written in one transaction; a lost race is retried once (AC-11).
/// </summary>
public sealed class ExternalSignInHandler(
    IUserRepository users,
    ISessionRevoker sessions,
    IEventOutbox outbox,
    IAuditLog audit,
    TimeProvider clock)
{
    public const string ExternalLoginLinkedEvent = "ExternalLoginLinked";
    public const string Failed = "identity.external_login_failed";
    public const string EmailUnverified = "identity.external_email_unverified";

    public async Task<Result<ExternalSignInOutcome>> HandleAsync(ExternalSignInCommand command, CancellationToken cancellationToken)
    {
        var identity = command.Identity;
        if (string.IsNullOrWhiteSpace(identity.Provider) || identity.Provider.Length > Domain.ExternalLogin.ProviderMaxLength
            || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > Domain.ExternalLogin.SubjectMaxLength)
        {
            return await AuditFailureAsync(Result<ExternalSignInOutcome>.Fail(Failed), cancellationToken).ConfigureAwait(false);
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var decided = await DecideAsync(identity, command.Language, cancellationToken).ConfigureAwait(false);
                return decided.IsSuccess ? decided : await AuditFailureAsync(decided, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt == 0 && ex is EmailInUseException or ExternalLoginConflictException)
            {
                // Another first sign-in for the same identity or email committed first: decide again, it is now known.
            }
            catch (Exception ex) when (ex is EmailInUseException or ExternalLoginConflictException)
            {
                return await AuditFailureAsync(Result<ExternalSignInOutcome>.Fail(Failed), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>One <c>sign_in_failed</c> row (method google, reason = the error code) for a failure decided here (NFR-SEC-009, D-2).</summary>
    private async Task<Result<ExternalSignInOutcome>> AuditFailureAsync(Result<ExternalSignInOutcome> failed, CancellationToken ct)
    {
        audit.Add(AuditEntry.SignInFailed(null, AuditMethod.Google, failed.Failure!.Code, clock.GetUtcNow()));
        await users.SaveChangesAsync(ct).ConfigureAwait(false);
        return failed;
    }

    private async Task<Result<ExternalSignInOutcome>> DecideAsync(ExternalIdentity identity, string language, CancellationToken ct)
    {
        // Known identity: sign in, write nothing, even when the provider's email changed since (D-4).
        if (await users.FindExternalLoginAsync(identity.Provider, identity.Subject!, ct).ConfigureAwait(false) is { } known)
        {
            var linked = await users.FindByIdAsync(known.UserId, ct).ConfigureAwait(false);
            return linked is null
                ? Result<ExternalSignInOutcome>.Fail(Failed)
                : await SignedInAsync(linked, await StampOfAsync(linked.Id, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        }

        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            return Result<ExternalSignInOutcome>.Fail(EmailUnverified);
        }

        var (email, violation) = Email.Create(identity.Email);
        if (violation is not null)
        {
            return Result<ExternalSignInOutcome>.Fail(Failed);
        }

        var now = clock.GetUtcNow();
        var existing = await users.FindByEmailAsync(email!.Normalized, ct).ConfigureAwait(false);
        return existing is null
            ? await CreateAsync(identity, email, language, now, ct).ConfigureAwait(false)
            : await LinkAsync(identity, existing, now, ct).ConfigureAwait(false);
    }

    /// <summary>FR-AUTH-021: link the identity to the account that owns the verified email.</summary>
    private async Task<Result<ExternalSignInOutcome>> LinkAsync(ExternalIdentity identity, User user, DateTimeOffset now, CancellationToken ct)
    {
        if (await users.FindExternalLoginByUserAsync(user.Id, identity.Provider, ct).ConfigureAwait(false) is { } current)
        {
            // The same identity was linked by a concurrent first sign-in since the lookup above: sign in. Another identity of the
            // provider on this account: refuse.
            return current.Subject == identity.Subject
                ? await SignedInAsync(user, await StampOfAsync(user.Id, ct).ConfigureAwait(false), ct).ConfigureAwait(false)
                : Result<ExternalSignInOutcome>.Fail(Failed);
        }

        var credential = await users.FindCredentialAsync(user.Id, ct).ConfigureAwait(false);
        var passwordRemoved = false;
        if (!user.IsEmailVerified)
        {
            // D-5: the provider proved the email, nobody proved the password. Drop it and every session made with it.
            user.VerifyEmail(now);
            if (credential is not null)
            {
                users.RemoveCredential(credential);
                credential = null;
                passwordRemoved = true;
            }

            foreach (var token in await users.FindPendingTokensAsync(user.Id, ct).ConfigureAwait(false))
            {
                token.Invalidate(now);
            }

            await sessions.RevokeOtherSessionsAsync(user.Id, null, ct).ConfigureAwait(false);
        }

        users.AddExternalLogin(Domain.ExternalLogin.Link(user.Id, identity.Provider, identity.Subject!, now));
        Enqueue(user, identity.Provider, accountCreated: false, passwordRemoved, now);
        audit.Add(AuditEntry.SignInSucceeded(user.Id, AuditMethod.Google, now));
        await users.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<ExternalSignInOutcome>.Ok(new ExternalSignInOutcome(user, credential?.Stamp));
    }

    /// <summary>FR-AUTH-020: a new verified Buyer with no password and no verification token.</summary>
    private async Task<Result<ExternalSignInOutcome>> CreateAsync(
        ExternalIdentity identity, Email email, string language, DateTimeOffset now, CancellationToken ct)
    {
        var (name, _) = DisplayName.Create(DisplayNameFrom(identity.Name, email.Value));
        var user = User.RegisterExternal(email, name!, language, now);
        users.Add(user);
        users.AddExternalLogin(Domain.ExternalLogin.Link(user.Id, identity.Provider, identity.Subject!, now));
        Enqueue(user, identity.Provider, accountCreated: true, passwordRemoved: false, now);
        audit.Add(AuditEntry.SignInSucceeded(user.Id, AuditMethod.Google, now));
        await users.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<ExternalSignInOutcome>.Ok(new ExternalSignInOutcome(user, null));
    }

    /// <summary>An existing identity signs in: the only thing written is the audit row (NFR-SEC-009).</summary>
    private async Task<Result<ExternalSignInOutcome>> SignedInAsync(User user, long? stamp, CancellationToken ct)
    {
        audit.Add(AuditEntry.SignInSucceeded(user.Id, AuditMethod.Google, clock.GetUtcNow()));
        await users.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<ExternalSignInOutcome>.Ok(new ExternalSignInOutcome(user, stamp));
    }

    /// <summary>The provider's name trimmed and cut to the limit, or the local part of the email when it has none.</summary>
    internal static string DisplayNameFrom(string? name, string email)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            value = email[..email.IndexOf('@', StringComparison.Ordinal)];
        }

        return value.Length > DisplayName.MaxLength ? value[..DisplayName.MaxLength].TrimEnd() : value;
    }

    private async Task<long?> StampOfAsync(Guid userId, CancellationToken ct) =>
        (await users.FindCredentialAsync(userId, ct).ConfigureAwait(false))?.Stamp;

    private void Enqueue(User user, string provider, bool accountCreated, bool passwordRemoved, DateTimeOffset now) =>
        outbox.Enqueue(
            ExternalLoginLinkedEvent,
            1,
            user.Id.ToString(),
            new ExternalLoginLinkedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, provider, accountCreated, passwordRemoved, now));
}
