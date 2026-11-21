using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Profile;

public sealed record UpdateMyLanguageCommand(Guid UserId, string? Language);

/// <summary>FR-PROF-006, FR-LOC-002: saves the language; the same value again is a success that queues nothing.</summary>
public sealed class UpdateMyLanguageHandler(IUserRepository users, IEventOutbox outbox, IClock clock)
{
    public async Task<Result<string>> HandleAsync(UpdateMyLanguageCommand command, CancellationToken cancellationToken)
    {
        var (language, violation) = Language.Create(command.Language);
        if (violation is not null)
        {
            return Result<string>.Fail("common.validation_failed", [violation]);
        }

        var user = await users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<string>.Fail("common.unauthenticated");
        }

        var changed = user.ChangeLanguage(language!, clock.UtcNow);
        if (changed.Count > 0)
        {
            outbox.Enqueue(
                ProfileEvents.UserProfileChanged,
                1,
                user.Id.ToString(),
                new UserProfileChangedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, changed));
            await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<string>.Ok(user.Language);
    }
}
