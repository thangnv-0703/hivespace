using System.Globalization;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// A date of birth, <c>YYYY-MM-DD</c>. It may not be after today nor more than 120 years before it; today is passed in
/// (the Application layer converts the clock to Asia/Ho_Chi_Minh, NFR-L10N-002).
/// </summary>
public sealed record DateOfBirth
{
    public const int MaxAgeYears = 120;

    private DateOfBirth(DateOnly value) => Value = value;

    public DateOnly Value { get; }

    public static (DateOfBirth? Date, Violation? Violation) Create(string? raw, DateOnly today)
    {
        if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return (null, new Violation("dateOfBirth", "identity.date_of_birth_invalid"));
        }

        if (date > today)
        {
            return (null, new Violation("dateOfBirth", "identity.date_of_birth_in_future"));
        }

        if (date < today.AddYears(-MaxAgeYears))
        {
            return (null, new Violation("dateOfBirth", "identity.date_of_birth_too_old"));
        }

        return (new DateOfBirth(date), null);
    }
}
