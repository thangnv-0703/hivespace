using System.Text;
using System.Text.RegularExpressions;
using HiveSpace.BuildingBlocks.Abstractions;

namespace Identity.Domain;

/// <summary>
/// Short free text used by the pickup address and the bank details: trimmed, inner whitespace collapsed to one space, NFC.
/// Control characters are rejected before collapsing, like <see cref="ShopName"/>. The caller picks the codes, so the generic
/// <c>identity.address_field_*</c> codes (shared with the buyer address book) and the bank ones use one rule.
/// </summary>
internal static partial class AddressText
{
    /// <param name="minLength">Values shorter than this (but not empty) report <paramref name="invalidCode"/>.</param>
    public static (string? Value, Violation? Violation) Create(
        string? raw, string field, int minLength, int maxLength, string requiredCode, string tooLongCode, string invalidCode)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return (null, new Violation(field, requiredCode));
        }

        if (trimmed.Any(char.IsControl))
        {
            return (null, new Violation(field, invalidCode));
        }

        var value = Whitespace().Replace(trimmed, " ").Normalize(NormalizationForm.FormC);
        if (value.Length < minLength)
        {
            return (null, new Violation(field, invalidCode));
        }

        return value.Length > maxLength ? (null, new Violation(field, tooLongCode)) : (value, null);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
