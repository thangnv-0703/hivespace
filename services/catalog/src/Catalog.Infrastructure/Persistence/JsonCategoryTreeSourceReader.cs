using System.Globalization;
using System.Text.Json;
using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Reads <c>categories.json</c> and <c>category-attributes/*.json</c> (design §3.4). Reports unreadable or malformed
/// files with file, line and position; fields the loader does not read are ignored.
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
        await ReadFileAsync(categoriesFile, "categories.json", problems, cancellationToken, root => ReadCategories(root, "categories.json", categories, problems)).ConfigureAwait(false);

        var entries = new List<RawCategoryAttributes>();
        var attributeDir = Path.Combine(sourcePath, "category-attributes");
        if (Directory.Exists(attributeDir))
        {
            foreach (var file in Directory.EnumerateFiles(attributeDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = $"category-attributes/{Path.GetFileName(file)}";
                await ReadFileAsync(file, name, problems, cancellationToken, root => ReadAttributeEntries(root, name, entries, problems)).ConfigureAwait(false);
            }
        }

        return new SourceReadResult(problems.Count > 0 ? null : new RawCategoryTree(categories, entries), problems);
    }

    private static async Task ReadFileAsync(string path, string name, List<Problem> problems, CancellationToken cancellationToken, Action<JsonElement> read)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, Options, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("categories", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new Problem("file.malformed", "file", name, null, "the file must be an object with a \"categories\" array"));
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

    private static void ReadAttributeEntries(JsonElement list, string file, List<RawCategoryAttributes> into, List<Problem> problems)
    {
        var index = 0;
        foreach (var entry in list.EnumerateArray())
        {
            var where = $"{file} item {index++}";
            var attributes = new List<RawAttribute>();
            if (entry.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Array)
            {
                var a = 0;
                foreach (var attr in attrs.EnumerateArray())
                {
                    attributes.Add(ReadAttribute(attr, $"{where} attribute {a++}", problems));
                }
            }
            else if (entry.TryGetProperty("attributes", out var other) && other.ValueKind != JsonValueKind.Null)
            {
                problems.Add(new Problem("file.malformed", "file", where, null, "\"attributes\" must be an array"));
            }

            into.Add(new RawCategoryAttributes(Key(entry, "externalCategoryId", where, problems), attributes));
        }
    }

    private static RawAttribute ReadAttribute(JsonElement attr, string where, List<Problem> problems)
    {
        var values = new List<RawValue>();
        if (attr.TryGetProperty("values", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            var v = 0;
            foreach (var value in list.EnumerateArray())
            {
                var valueWhere = $"{where} value {v++}";
                values.Add(new RawValue(Key(value, "sourceValueId", valueWhere, problems), Name(value, valueWhere, problems)));
            }
        }
        else if (list.ValueKind != JsonValueKind.Undefined && list.ValueKind != JsonValueKind.Null)
        {
            problems.Add(new Problem("file.malformed", "file", where, null, "\"values\" must be an array"));
        }

        var required = attr.TryGetProperty("isRequired", out var r) && r.ValueKind is JsonValueKind.True;
        if (attr.TryGetProperty("isRequired", out var rr) && rr.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
        {
            problems.Add(new Problem("file.malformed", "file", where, null, "\"isRequired\" must be true or false"));
        }

        var max = 0;
        if (attr.TryGetProperty("maxValueCount", out var m) && m.ValueKind != JsonValueKind.Null && !(m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out max)))
        {
            problems.Add(new Problem("file.malformed", "file", where, null, "\"maxValueCount\" must be a whole number"));
        }

        return new RawAttribute(
            Key(attr, "sourceAttributeId", where, problems), Name(attr, where, problems), required, Text(attr, "inputType", where, problems), max, values);
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
