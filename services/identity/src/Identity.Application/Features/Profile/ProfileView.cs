using Identity.Domain;

namespace Identity.Application.Features.Profile;

/// <summary>The <c>Profile</c> schema of <c>identity.yaml</c>. <see cref="AvatarUrl"/> stays null until FEAT-035.</summary>
public sealed record ProfileView(
    Guid UserId,
    string Email,
    string DisplayName,
    string? Phone,
    string? Gender,
    string? DateOfBirth,
    string? AvatarUrl,
    string Language,
    bool HasPassword)
{
    public static ProfileView From(User user, bool hasPassword) => new(
        user.Id,
        user.EmailAddress,
        user.DisplayName,
        user.Phone,
        user.Gender,
        user.DateOfBirth?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        null,
        user.Language,
        hasPassword);
}

/// <summary>Payload of <c>UserProfileChanged/v1</c>. Phone, gender and date of birth are deliberately absent (D-6, NFR-LEG-001).</summary>
public sealed record UserProfileChangedData(Guid UserId, string Email, string DisplayName, string Language, IReadOnlyList<string> ChangedFields);

internal static class ProfileEvents
{
    public const string UserProfileChanged = "UserProfileChanged";

    /// <summary>Vietnam has no daylight saving, so a fixed UTC+7 equals Asia/Ho_Chi_Minh (NFR-L10N-002).</summary>
    public static DateOnly TodayInVietnam(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
}
