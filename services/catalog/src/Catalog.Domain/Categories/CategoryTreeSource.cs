using System.Globalization;

namespace Catalog.Domain.Categories;

public sealed record SourceBuildResult(CategoryTreeSource? Source, IReadOnlyList<Problem> Problems);

/// <summary>
/// The parsed, cleaned load file: categories, attributes, values and links keyed by their integer source ids, in file order.
/// <see cref="Build"/> reports every file-integrity problem and never throws (design §3.5, §5.2).
/// </summary>
public sealed class CategoryTreeSource
{
    private const int MaxCategoryName = 200;
    private const int MaxAttributeName = 200;
    private const int MaxValueName = 300;
    private const int MaxImageUrl = 500;

    private CategoryTreeSource(
        IReadOnlyList<SourceCategory> categories,
        IReadOnlyList<SourceAttribute> attributes,
        IReadOnlyList<SourceValue> values,
        IReadOnlyList<SourceLink> links)
    {
        Categories = categories;
        Attributes = attributes;
        Values = values;
        Links = links;
    }

    public IReadOnlyList<SourceCategory> Categories { get; }

    public IReadOnlyList<SourceAttribute> Attributes { get; }

    public IReadOnlyList<SourceValue> Values { get; }

    public IReadOnlyList<SourceLink> Links { get; }

    /// <summary>Returns the source when the file has no problem, otherwise every problem found and no source.</summary>
    public static SourceBuildResult Build(RawCategoryTree raw)
    {
        var problems = new List<Problem>();
        var categories = BuildCategories(raw.Categories, problems);
        var (attributes, values, definedKeys) = BuildDefinitions(raw.Attributes, problems);
        var links = BuildLinks(raw.AttributeEntries, categories.Select(c => c.Id).ToHashSet(), definedKeys, problems);
        return problems.Count > 0
            ? new SourceBuildResult(null, problems)
            : new SourceBuildResult(new CategoryTreeSource(categories, attributes, values, links), problems);
    }

    private static bool TryKey(string? text, out int key) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out key) && key >= 1;

    private static LocalizedName? CleanName(RawName raw, string kind, string key, int? categoryId, int max, List<Problem> problems)
    {
        var vi = NameCleaner.Clean(raw.Vi);
        var en = NameCleaner.Clean(raw.En);
        var ok = true;
        foreach (var (language, value) in new[] { ("vi", vi), ("en", en) })
        {
            if (value is null)
            {
                problems.Add(new Problem("name.missing", kind, key, categoryId, $"{language} is missing"));
                ok = false;
            }
            else if (value.Length > max)
            {
                problems.Add(new Problem("name.too_long", kind, key, categoryId, $"{language} is {value.Length} characters, the limit is {max}"));
                ok = false;
            }
        }

        return ok ? new LocalizedName(vi!, en!) : null;
    }

    private static List<SourceCategory> BuildCategories(IReadOnlyList<RawCategory> raw, List<Problem> problems)
    {
        var parsed = new List<(int Id, int? ParentId, LocalizedName Name, string? ImageUrl)>();
        var empty = new LocalizedName(string.Empty, string.Empty);
        var seen = new HashSet<int>();
        foreach (var c in raw)
        {
            var key = c.Key ?? string.Empty;
            var valid = true;
            if (!TryKey(c.Key, out var id))
            {
                problems.Add(new Problem("category.key_invalid", "category", key, null, "the key is not a whole number from 1 to 2147483647"));
                valid = false;
            }
            else if (!seen.Add(id))
            {
                problems.Add(new Problem("category.duplicate_key", "category", key, null, "the key appears more than once"));
                valid = false;
            }

            int? parentId = null;
            var isTopLevel = string.IsNullOrWhiteSpace(c.ParentKey);
            if (!isTopLevel)
            {
                if (TryKey(c.ParentKey, out var p))
                {
                    parentId = p;
                }
                else
                {
                    problems.Add(new Problem("category.key_invalid", "category", key, null, $"the parent key \"{c.ParentKey}\" is not a whole number from 1 to 2147483647"));
                    valid = false;
                }
            }

            var name = CleanName(c.Name, "category", key, null, MaxCategoryName, problems);

            // An image is read only on top-level categories; below them it is dropped without a problem (AC-014-5.4).
            string? image = null;
            if (isTopLevel && c.ImageUrl is not null)
            {
                if (c.ImageUrl.Length <= MaxImageUrl && Uri.TryCreate(c.ImageUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    image = c.ImageUrl;
                }
                else
                {
                    problems.Add(new Problem("category.image_invalid", "category", key, null, "imageUrl must be an absolute https URL of at most 500 characters"));
                    valid = false;
                }
            }

            // A category with a bad name stays in the list so its children and attribute entries do not also report it as unknown.
            if (valid)
            {
                parsed.Add((id, parentId, name ?? empty, image));
            }
        }

        var ids = parsed.Select(c => c.Id).ToHashSet();
        foreach (var c in parsed)
        {
            if (c.ParentId is { } parent && !ids.Contains(parent))
            {
                problems.Add(new Problem("category.parent_unknown", "category", c.Id.ToString(CultureInfo.InvariantCulture), null, $"parent {parent} is not in the file"));
            }
        }

        ReportCycles(parsed.Where(c => c.ParentId is null || ids.Contains(c.ParentId.Value)).ToDictionary(c => c.Id, c => c.ParentId), problems);

        // Position is the index among siblings in file order; a leaf has no children (BR-014-2, BR-014-3).
        var hasChildren = parsed.Where(c => c.ParentId is not null).Select(c => c.ParentId!.Value).ToHashSet();
        var nextPosition = new Dictionary<int, int>();
        var result = new List<SourceCategory>(parsed.Count);
        foreach (var c in parsed)
        {
            var group = c.ParentId ?? 0;
            var position = nextPosition.GetValueOrDefault(group);
            nextPosition[group] = position + 1;
            result.Add(new SourceCategory(c.Id, c.ParentId, position, c.Name, !hasChildren.Contains(c.Id), c.ImageUrl));
        }

        return result;
    }

    private static void ReportCycles(Dictionary<int, int?> parentOf, List<Problem> problems)
    {
        var done = new HashSet<int>();
        foreach (var start in parentOf.Keys)
        {
            var path = new List<int>();
            var onPath = new HashSet<int>();
            int? current = start;
            while (current is { } id && !done.Contains(id))
            {
                if (!onPath.Add(id))
                {
                    problems.Add(new Problem("category.cycle", "category", id.ToString(CultureInfo.InvariantCulture), null, "the category is its own ancestor"));
                    break;
                }

                path.Add(id);
                current = parentOf.GetValueOrDefault(id);
            }

            done.UnionWith(path);
        }
    }

    /// <summary>
    /// Builds each attribute of <c>attributes.json</c> once, with its list settings and values in file order (D-11, D-13).
    /// Returns every attribute key the file defines, even one with a problem, so its links are not also reported as unknown.
    /// </summary>
    private static (List<SourceAttribute>, List<SourceValue>, HashSet<int>) BuildDefinitions(IReadOnlyList<RawAttributeDefinition> raw, List<Problem> problems)
    {
        var attributes = new List<SourceAttribute>();
        var values = new List<SourceValue>();
        var definedKeys = new HashSet<int>();
        var valueOwner = new Dictionary<int, int>();

        foreach (var a in raw)
        {
            var key = a.Key ?? string.Empty;
            if (!TryKey(a.Key, out var attributeId))
            {
                problems.Add(new Problem("attribute.key_invalid", "attribute", key, null, "the key is not a whole number from 1 to 2147483647"));
                continue;
            }

            if (!definedKeys.Add(attributeId))
            {
                problems.Add(new Problem("attribute.duplicate_key", "attribute", key, null, "the key appears more than once in attributes.json"));
                continue;
            }

            var ok = true;
            var name = CleanName(a.Name, "attribute", key, null, MaxAttributeName, problems);
            ok &= name is not null;

            var attributeValues = new List<SourceValue>();
            var valueNames = (Vi: new HashSet<string>(StringComparer.OrdinalIgnoreCase), En: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            foreach (var v in a.Values)
            {
                var valueKey = v.Key ?? string.Empty;
                if (!TryKey(v.Key, out var valueId))
                {
                    problems.Add(new Problem("value.key_invalid", "value", valueKey, null, $"the key (of attribute {key}) is not a whole number from 1 to 2147483647"));
                    ok = false;
                    continue;
                }

                if (attributeValues.Any(x => x.Id == valueId))
                {
                    problems.Add(new Problem("value.duplicate_key", "value", valueKey, null, $"the key appears twice in the list of attribute {key}"));
                    ok = false;
                    continue;
                }

                if (valueOwner.TryGetValue(valueId, out var owner))
                {
                    problems.Add(new Problem("value.other_attribute", "value", valueKey, null, $"the key of attribute {key} is also used under attribute {owner}"));
                    ok = false;
                    continue;
                }

                valueOwner[valueId] = attributeId;
                var valueName = CleanName(v.Name, "value", valueKey, null, MaxValueName, problems);
                if (valueName is null)
                {
                    ok = false;
                    continue;
                }

                if (!valueNames.Vi.Add(valueName.Vi) || !valueNames.En.Add(valueName.En))
                {
                    problems.Add(new Problem("value.duplicate_name", "value", valueKey, null, $"another value of attribute {key} has the same name (\"{valueName.Vi}\" / \"{valueName.En}\")"));
                    ok = false;
                    continue;
                }

                attributeValues.Add(new SourceValue(valueId, attributeId, attributeValues.Count, valueName));
            }

            // No values means free text: inputType, maxValueCount and allowsCustomValue are then ignored (BR-014-8).
            AttributeKind kind;
            bool allowsMultiple;
            int maxValues;
            if (a.Values.Count == 0)
            {
                (kind, allowsMultiple, maxValues) = (AttributeKind.Text, false, 1);
            }
            else if (a.InputType == "Dropdown")
            {
                (kind, allowsMultiple, maxValues) = (AttributeKind.List, false, 1);
            }
            else if (a.InputType == "Checkbox" && a.MaxValueCount >= 2)
            {
                (kind, allowsMultiple, maxValues) = (AttributeKind.List, true, a.MaxValueCount);
            }
            else
            {
                problems.Add(new Problem("attribute.invalid_selection", "attribute", key, null, $"inputType \"{a.InputType}\" with maxValueCount {a.MaxValueCount} is not Dropdown, or Checkbox with a maximum of at least 2"));
                continue;
            }

            if (ok)
            {
                var allowsCustomValue = kind == AttributeKind.List && a.AllowsCustomValue;
                attributes.Add(new SourceAttribute(attributeId, name!, kind, allowsMultiple, maxValues, allowsCustomValue, [.. attributeValues.Select(x => x.Id)]));
                values.AddRange(attributeValues);
            }
        }

        return (attributes, values, definedKeys);
    }

    /// <summary>Builds each category's links in file order; a link only names an attribute of <c>attributes.json</c>.</summary>
    private static List<SourceLink> BuildLinks(
        IReadOnlyList<RawCategoryAttributes> entries, HashSet<int> categoryIds, HashSet<int> definedKeys, List<Problem> problems)
    {
        var links = new List<SourceLink>();
        var seenCategories = new HashSet<int>();

        foreach (var entry in entries)
        {
            var categoryKey = entry.CategoryKey ?? string.Empty;
            if (!TryKey(entry.CategoryKey, out var categoryId))
            {
                problems.Add(new Problem("category.key_invalid", "category", categoryKey, null, "the key in the attribute file is not a whole number from 1 to 2147483647"));
                continue;
            }

            if (!categoryIds.Contains(categoryId))
            {
                problems.Add(new Problem("attributes.category_unknown", "category", categoryKey, null, "the attribute entry is for a category that is not in categories.json"));
                continue;
            }

            if (!seenCategories.Add(categoryId))
            {
                problems.Add(new Problem("attributes.category_duplicate", "category", categoryKey, null, "the category has more than one attribute entry"));
                continue;
            }

            var seenAttributes = new HashSet<int>();
            var position = 0;
            foreach (var link in entry.Links)
            {
                var key = link.Key ?? string.Empty;
                if (!TryKey(link.Key, out var attributeId))
                {
                    problems.Add(new Problem("attribute.key_invalid", "attribute", key, categoryId, "the key is not a whole number from 1 to 2147483647"));
                    continue;
                }

                if (!definedKeys.Contains(attributeId))
                {
                    problems.Add(new Problem("attributes.unknown", "attribute", key, categoryId, "the link names an attribute that is not in attributes.json"));
                    continue;
                }

                if (!seenAttributes.Add(attributeId))
                {
                    problems.Add(new Problem("attribute.duplicate_in_category", "attribute", key, categoryId, "the attribute appears more than once in the category"));
                    continue;
                }

                links.Add(new SourceLink(categoryId, attributeId, position++, link.IsRequired));
            }
        }

        return links;
    }
}
