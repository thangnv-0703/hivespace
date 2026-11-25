using System.Text.RegularExpressions;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// A Vietnamese phone number kept in E.164 form <c>+84xxxxxxxxx</c> (D-3). Spaces, dots and dashes are ignored; accepted
/// written forms are <c>0</c> or <c>+84</c> followed by 9 digits whose first is 2 (landline) or 3, 5, 7, 8, 9 (mobile).
/// </summary>
public sealed partial record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    public string Value { get; }

    public static (PhoneNumber? Phone, Violation? Violation) Create(string? raw, string field = "phone")
    {
        var compact = Separators().Replace(raw ?? string.Empty, string.Empty);
        var match = Shape().Match(compact);
        return match.Success
            ? (new PhoneNumber("+84" + match.Groups["national"].Value), null)
            : (null, new Violation(field, "identity.phone_invalid"));
    }

    /// <summary>Wraps a value read back from the database, which was normalized when it was saved.</summary>
    public static PhoneNumber FromStored(string value) => new(value);

    [GeneratedRegex(@"[ .\-]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"^(?:0|\+84)(?<national>[235789]\d{8})$")]
    private static partial Regex Shape();
}
