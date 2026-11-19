using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Registration;

public sealed record RegisterUserCommand(string? Email, string? DisplayName, string? Password, string Language);

/// <summary>Payload of <c>UserRegistered/v1</c> and <c>VerificationEmailRequested/v1</c> (contracts/events). The token is never logged (R-6).</summary>
public sealed record VerificationEmailData(
    Guid UserId,
    string Email,
    string DisplayName,
    string Language,
    string VerificationToken,
    DateTimeOffset VerificationTokenExpiresAt);

/// <summary>FR-AUTH-001..004: validates everything at once, then writes user, credential, Buyer role, token hash and outbox row in one transaction.</summary>
public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IVerificationTokenGenerator tokens,
    IEventOutbox outbox,
    IClock clock)
{
    public const string UserRegisteredEvent = "UserRegistered";

    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var violations = new List<Violation>();
        var (email, emailViolation) = Email.Create(command.Email);
        var (name, nameViolation) = DisplayName.Create(command.DisplayName);
        if (emailViolation is not null)
        {
            violations.Add(emailViolation);
        }

        if (nameViolation is not null)
        {
            violations.Add(nameViolation);
        }

        violations.AddRange(PasswordPolicy.Evaluate(command.Password));
        if (violations.Count > 0)
        {
            return Result<Guid>.Fail("common.validation_failed", violations);
        }

        if (await users.EmailExistsAsync(email!.Normalized, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Fail("identity.email_in_use");
        }

        var now = clock.UtcNow;
        var user = User.Register(email, name!, command.Language, now);
        var credential = Credential.Create(user.Id, hasher.Hash(command.Password!), now);
        var generated = tokens.Generate();
        var token = VerificationToken.Issue(user.Id, generated.Hash, now);

        users.Add(user, credential, token);
        outbox.Enqueue(
            UserRegisteredEvent,
            1,
            user.Id.ToString(),
            new VerificationEmailData(user.Id, user.EmailAddress, user.DisplayName, user.Language, generated.Token, token.ExpiresAt));

        try
        {
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EmailInUseException)
        {
            return Result<Guid>.Fail("identity.email_in_use");
        }

        return Result<Guid>.Ok(user.Id);
    }
}
