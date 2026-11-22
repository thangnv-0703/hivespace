using HiveSpace.BuildingBlocks.Abstractions;
using Identity.Application.Abstractions;
using Identity.Domain;

namespace Identity.Application.Features.Profile;

/// <summary>
/// The full editable representation (PUT). <paramref name="UnknownKeys"/> are body keys outside the contract, found by the Api
/// before binding: <c>email</c> is read-only (FR-PROF-004), any other is a plain validation failure.
/// </summary>
public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string? DisplayName,
    string? Phone,
    string? Gender,
    string? DateOfBirth,
    IReadOnlyList<string> UnknownKeys);

/// <summary>FR-PROF-002, FR-PROF-003: validates every field at once, then saves; queues <c>UserProfileChanged</c> only for a display name change.</summary>
public sealed class UpdateMyProfileHandler(IUserRepository users, IEventOutbox outbox, TimeProvider clock)
{
    public async Task<Result<ProfileView>> HandleAsync(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var violations = new List<Violation>();

        foreach (var key in command.UnknownKeys)
        {
            violations.Add(key == "email"
                ? new Violation("email", "identity.email_read_only")
                : new Violation(key, ErrorCodes.ValidationFailed));
        }

        var (name, nameViolation) = DisplayName.Create(command.DisplayName);
        AddIfAny(violations, nameViolation);

        PhoneNumber? phone = null;
        if (command.Phone is not null)
        {
            var (parsed, violation) = PhoneNumber.Create(command.Phone);
            phone = parsed;
            AddIfAny(violations, violation);
        }

        Gender? gender = null;
        if (command.Gender is not null)
        {
            var (parsed, violation) = Gender.Create(command.Gender);
            gender = parsed;
            AddIfAny(violations, violation);
        }

        DateOfBirth? dateOfBirth = null;
        if (command.DateOfBirth is not null)
        {
            var (parsed, violation) = DateOfBirth.Create(command.DateOfBirth, ProfileEvents.TodayInVietnam(now));
            dateOfBirth = parsed;
            AddIfAny(violations, violation);
        }

        if (violations.Count > 0)
        {
            return Result<ProfileView>.Fail(ErrorCodes.ValidationFailed, violations);
        }

        var user = await users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result<ProfileView>.Fail(ErrorCodes.Unauthenticated);
        }

        var changed = user.UpdateProfile(name!, phone, gender, dateOfBirth, now);
        if (changed.Count > 0)
        {
            outbox.Enqueue(
                ProfileEvents.UserProfileChanged,
                1,
                user.Id.ToString(),
                new UserProfileChangedData(user.Id, user.EmailAddress, user.DisplayName, user.Language, changed));
        }

        await users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ProfileView>.Ok(ProfileView.From(user));
    }

    private static void AddIfAny(List<Violation> violations, Violation? violation)
    {
        if (violation is not null)
        {
            violations.Add(violation);
        }
    }
}
