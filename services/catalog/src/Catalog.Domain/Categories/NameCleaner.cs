using System.Globalization;
using System.Text;

namespace Catalog.Domain.Categories;

/// <summary>Cleans names and values read from the load file (BR-014-11).</summary>
public static class NameCleaner
{
    /// <summary>
    /// NFC-normalizes, drops control and format characters (zero-width, BOM), turns other whitespace (tab, newline)
    /// into a space, collapses runs of spaces and trims. Returns <c>null</c> when nothing is left.
    /// </summary>
    public static string? Clean(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);
        var pendingSpace = false;
        foreach (var c in raw.Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
