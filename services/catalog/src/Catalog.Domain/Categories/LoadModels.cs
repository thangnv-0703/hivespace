namespace Catalog.Domain.Categories;

// What the file reader hands to the domain: keys and names exactly as read, nothing validated yet (v2 layout, D-13).
public sealed record RawName(string? Vi, string? En);

public sealed record RawCategory(string? Key, string? ParentKey, RawName Name, string? ImageUrl);

public sealed record RawValue(string? Key, RawName Name);

/// <summary>One attribute of <c>attributes.json</c>: defined once, with its list settings and its values in display order.</summary>
public sealed record RawAttributeDefinition(
    string? Key, RawName Name, string? InputType, int MaxValueCount, bool AllowsCustomValue, IReadOnlyList<RawValue> Values);

/// <summary>One link of a category to an attribute, in the category's display order.</summary>
public sealed record RawLink(string? Key, bool IsRequired);

public sealed record RawCategoryAttributes(string? CategoryKey, IReadOnlyList<RawLink> Links);

public sealed record RawCategoryTree(
    IReadOnlyList<RawCategory> Categories, IReadOnlyList<RawAttributeDefinition> Attributes, IReadOnlyList<RawCategoryAttributes> AttributeEntries);

/// <summary>One thing wrong with a load, named by code, kind and key (design §3.5).</summary>
public sealed record Problem(string Code, string Kind, string Key, int? CategoryId, string Detail)
{
    public override string ToString() =>
        $"[{Code}] {Kind} {Key}{(CategoryId is { } c && Kind != "category" ? $" in category {c}" : string.Empty)}: {Detail}";
}

// The cleaned file, keyed by integer ids, in file order.
public sealed record SourceCategory(int Id, int? ParentId, int Position, LocalizedName Name, bool IsLeaf, string? ImageUrl);

/// <summary>An attribute with its list settings and its value ids in display order (empty exactly for text).</summary>
public sealed record SourceAttribute(
    int Id, LocalizedName Name, AttributeKind Kind, bool AllowsMultiple, int MaxValues, bool AllowsCustomValue, IReadOnlyList<int> ValueIds);

public sealed record SourceValue(int Id, int AttributeId, int Position, LocalizedName Name);

public sealed record SourceLink(int CategoryId, int AttributeId, int Position, bool IsRequired);

/// <summary>The stored tree, as the diff compares it with the file.</summary>
public sealed record CurrentTree(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<AttributeDefinition> Attributes,
    IReadOnlyList<AttributeValue> Values,
    IReadOnlyList<CategoryAttribute> Links);
