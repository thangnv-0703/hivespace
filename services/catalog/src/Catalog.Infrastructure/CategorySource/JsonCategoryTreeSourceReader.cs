using System.Globalization;
using System.Text.Json;
using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;

namespace Catalog.Infrastructure.CategorySource;

/// <summary>
/// Reads the v2 layout (design §3.4, D-13): <c>categories.json</c>, <c>attributes.json</c> (each attribute once, with its list
/// settings and values) and link-only <c>category-attributes/*.json</c>. Reports unreadable or malformed files with file,
/// line and position; fields the loader does not read are ignored, so a link cannot redefine a list.
/// </summary>
public sealed class JsonCategoryTreeSourceReader : ICategoryTreeSourceReader
{
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public async Task<SourceReadResult> ReadAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var problems = new List<Problem>();
        var categoriesFile = Path.Combine(sourcePath, "categories.json");
        if (!File.Exists(categoriesFile))
        {
            problems.Add(new Problem("file.unreadable", "file", "categories.json", null, $"{categoriesFile} does not exist"));
            return new SourceReadResult(null, problems);
        }

        var categories = new List<RawCategory>();
        await ReadFileAsync(categoriesFile, "categories.json", "categories", problems, cancellationToken, root => ReadCategories(root, "categories.json", categories, problems)).ConfigureAwait(false);

        // No attributes.json means no attributes; any link then names an unknown attribute.
        var attributes = new List<RawAttributeDefinition>();
        var attributesFile = Path.Combine(sourcePath, "attributes.json");
        if (File.Exists(attributesFile))
        {
            await ReadFileAsync(attributesFile, "attributes.json", "attributes", problems, cancellationToken, root => ReadDefinitions(root, "attributes.json", attributes, problems)).ConfigureAwait(false);
        }

        var entries = new List<RawCategoryAttributes>();
        var attributeDir = Path.Combine(sourcePath, "category-attributes");
        if (Directory.Exists(attributeDir))
        {
            foreach (var file in Directory.EnumerateFiles(attributeDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = $"category-attributes/{Path.GetFileName(file)}";
                await ReadFileAsync(file, name, "categories", problems, cancellationToken, root => ReadLinkEntries(root, name, entries, problems)).ConfigureAwait(false);
            }
        }

        return new SourceReadResult(problems.Count > 0 ? null : new RawCategoryTree(categories, attributes, entries), problems);
    }

    private static async Task ReadFileAsync(
        string path, string name, string listProperty, List<Problem> problems, CancellationToken cancellationToken, Action<JsonElement> read)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, Options, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(listProperty, out var list) || list.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new Problem("file.malformed", "file", name, null, $"the file must be an object with a \"{listProperty}\" array"));
                return;
            }

            read(list);
        }
        catch (JsonException ex)
        {
            problems.Add(new Problem("file.malformed", "file", name, null, $"invalid JSON at line {ex.LineNumber! + 1}, position {ex.BytePositionInLine! + 1}: {ex.Message}"));
        }
        catch (IOException ex)
        {
            problems.Add(new Problem("file.unreadable", "file", name, null, ex.Message));
        }
    }

    private static void ReadCategories(JsonElement list, string file, List<RawCategory> into, List<Problem> problems)
    {
        var index = 0;
        foreach (var item in list.EnumerateArray())
        {
            var where = $"{file} item {index++}";
            var key = Key(item, "externalCategoryId", where, problems);
            var parent = Key(item, "externalParentCategoryId", where, problems);
            var name = Name(item, where, problems);

            // Wrong type for an image is a problem on top-level categories only; below them the link is ignored (AC-014-5.4).
            string? image = null;
            if (item.TryGetProperty("imageUrl", out var img) && img.ValueKind != JsonValueKind.Null)
            {
                if (img.ValueKind == JsonValueKind.String)
                {
                    image = img.GetString();
                }
                else if (string.IsNullOrWhiteSpace(parent))
                {
                    problems.Add(new Problem("category.image_invalid", "category", key ?? where, null, "imageUrl must be a string"));
                }
            }

            into.Add(new RawCategory(key, parent, name, image));
        }
    }

    private static void ReadDefinitions(JsonElement list, string file, List<RawAttributeDefinition> into, List<Problem> problems)
    {
        var index = 0;
        foreach (var attr in list.EnumerateArray())
        {
            var where = $"{file} item {index++}";
            var values = new List<RawValue>();
            if (attr.TryGetProperty("values", out var valueList) && valueList.ValueKind == JsonValueKind.Array)
            {
                var v = 0;
                foreach (var value in valueList.EnumerateArray())
                {
                    var valueWhere = $"{where} value {v++}";
                    values.Add(new RawValue(Key(value, "sourceValueId", valueWhere, problems), Name(value, valueWhere, problems)));
                }
            }
            else if (valueList.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                problems.Add(new Problem("file.malformed", "file", where, null, "\"values\" must be an array"));
            }

            var max = 0;
            if (attr.TryGetProperty("maxValueCount", out var m) && m.ValueKind != JsonValueKind.Null && !(m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out max)))
            {
                problems.Add(new Problem("file.malformed", "file", where, null, "\"maxValueCount\" must be a whole number"));
            }

            into.Add(new RawAttributeDefinition(
                Key(attr, "sourceAttributeId", where, problems), Name(attr, where, problems), Text(attr, "inputType", where, problems), max,
                Flag(attr, "allowsCustomValue", where, problems), values));
        }
    }

    private static void ReadLinkEntries(JsonElement list, string file, List<RawCategoryAttributes> into, List<Problem> problems)
    {
        var index = 0;
        foreach (var entry in list.EnumerateArray())
        {
            var where = $"{file} item {index++}";
            var links = new List<RawLink>();
            if (entry.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
            {
                var a = 0;
                foreach (var link in attrs.EnumerateArray())
                {
                    // Only the key and the required flag are read; name, values or inputType on a link are ignored (D-13).
                    var linkWhere = $"{where} attribute {a++}";
                    links.Add(new RawLink(Key(link, "sourceAttributeId", linkWhere, problems), Flag(link, "isRequired", linkWhere, problems)));
                }
            }
            else if (attrs.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                problems.Add(new Problem("file.malformed", "file", where, null, "\"attributes\" must be an array"));
            }

            into.Add(new RawCategoryAttributes(Key(entry, "externalCategoryId", where, problems), links));
        }
    }

    /// <summary>An optional boolean; absent or null is false.</summary>
    private static bool Flag(JsonElement item, string property, string where, List<Problem> problems)
    {
        if (!item.TryGetProperty(property, out var value))
        {
            return false;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
        {
            problems.Add(new Problem("file.malformed", "file", where, null, $"\"{property}\" must be true or false"));
        }

        return value.ValueKind == JsonValueKind.True;
    }

    private static RawName Name(JsonElement item, string where, List<Problem> problems)
    {
        if (!item.TryGetProperty("name", out var name) || name.ValueKind == JsonValueKind.Null)
        {
            return new RawName(null, null);
        }

        if (name.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new Problem("file.malformed", "file", where, null, "\"name\" must be an object with vi and en"));
            return new RawName(null, null);
        }

        return new RawName(Text(name, "vi", where, problems), Text(name, "en", where, problems));
    }

    private static string? Text(JsonElement item, string property, string where, List<Problem> problems)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        problems.Add(new Problem("file.malformed", "file", where, null, $"\"{property}\" must be a string"));
        return null;
    }

    /// <summary>A key is a string or a number; it is kept as text and checked when the source is built.</summary>
    private static string? Key(JsonElement item, string property, string where, List<Problem> problems)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();
            case JsonValueKind.Number:
                return value.GetRawText();
            default:
                problems.Add(new Problem("file.malformed", "file", where, null, $"\"{property}\" must be a string or a number"));
                return null;
        }
    }
}
