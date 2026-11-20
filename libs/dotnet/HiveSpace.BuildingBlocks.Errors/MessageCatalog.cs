using System.Globalization;
using System.Reflection;

namespace HiveSpace.BuildingBlocks.Errors;

/// <summary>
/// Error codes, HTTP statuses and vi/en messages loaded from the embedded <c>contracts/errors</c> files.
/// Vietnamese is the fallback for any unknown language (D-2).
/// </summary>
public static class MessageCatalog
{
    public const string DefaultLanguage = "vi";

    private static readonly Lazy<Catalog> Loaded = new(Load);

    public static IReadOnlyCollection<string> Codes => Loaded.Value.Statuses.Keys;

    public static bool IsKnown(string code) => Loaded.Value.Statuses.ContainsKey(code);

    /// <summary>The HTTP status registered for a code; unknown codes are treated as 500.</summary>
    public static int StatusOf(string code) =>
        Loaded.Value.Statuses.TryGetValue(code, out var status) ? status : 500;

    /// <summary>The message in <paramref name="language"/>, falling back to Vietnamese, then to the code itself.</summary>
    public static string MessageOf(string code, string language)
    {
        var messages = Loaded.Value.Messages;
        if (messages.TryGetValue(language, out var byLanguage) && byLanguage.TryGetValue(code, out var text))
        {
            return text;
        }

        return messages[DefaultLanguage].TryGetValue(code, out var fallback) ? fallback : code;
    }

    /// <summary>Messages for one language; used by tests to check key parity.</summary>
    public static IReadOnlyDictionary<string, string> MessagesFor(string language) => Loaded.Value.Messages[language];

    private static Catalog Load()
    {
        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in ReadLines("errors.error-codes.yaml"))
        {
            // "  common.not_found: { http: 404 }"
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            var brace = line.IndexOf("http:", StringComparison.Ordinal);
            if (colon <= 0 || brace < 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var code = line[..colon].Trim();
            var number = line[(brace + 5)..].Trim(' ', '}', '\t');
            statuses[code] = int.Parse(number, CultureInfo.InvariantCulture);
        }

        var messages = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in new[] { "vi", "en" })
        {
            var byCode = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in ReadLines($"errors.messages.{language}.yaml"))
            {
                var colon = line.IndexOf(": ", StringComparison.Ordinal);
                if (colon <= 0 || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                var text = line[(colon + 2)..].Trim();
                if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
                {
                    text = text[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
                }

                byCode[line[..colon].Trim()] = text;
            }

            messages[language] = byCode;
        }

        return new Catalog(statuses, messages);
    }

    private static IEnumerable<string> ReadLines(string resource)
    {
        using var stream = typeof(MessageCatalog).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
    }

    private sealed record Catalog(
        Dictionary<string, int> Statuses,
        Dictionary<string, IReadOnlyDictionary<string, string>> Messages);
}
