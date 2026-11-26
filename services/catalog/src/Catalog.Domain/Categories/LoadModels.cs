namespace Catalog.Domain.Categories;

// What the file reader hands to the domain: keys and names exactly as read, nothing validated yet.
public sealed record RawName(string? Vi, string? En);

public sealed record RawCategory(string? Key, string? ParentKey, RawName Name, string? ImageUrl);

public sealed record RawValue(string? Key, RawName Name);

public sealed record RawAttribute(string? Key, RawName Name, bool IsRequired, string? InputType, int MaxValueCount, IReadOnlyList<RawValue> Values);

public sealed record RawCategoryAttributes(string? CategoryKey, IReadOnlyList<RawAttribute> Attributes);

public sealed record RawCategoryTree(IReadOnlyList<RawCategory> Categories, IReadOnlyList<RawCategoryAttributes> AttributeEntries);

/// <summary>One thing wrong with a load, named by code, kind and key (design §3.5).</summary>
public sealed record Problem(string Code, string Kind, string Key, int? CategoryId, string Detail)
{
    public override string ToString() =>
        $"[{Code}] {Kind} {Key}{(CategoryId is { } c && Kind != "category" ? $" in category {c}" : string.Empty)}: {Detail}";
}

// The cleaned file, keyed by integer ids, in file order.
public sealed record SourceCategory(int Id, int? ParentId, int Position, LocalizedName Name, bool IsLeaf, string? ImageUrl);

public sealed record SourceAttribute(int Id, LocalizedName Name);

public sealed record SourceValue(int Id, int AttributeId, LocalizedName Name);

public sealed record SourceLink(
    int CategoryId, int AttributeId, int Position, bool IsRequired, AttributeKind Kind, bool AllowsMultiple, int MaxValues, IReadOnlyList<int> ValueIds);

/// <summary>The stored tree, as the diff compares it with the file.</summary>
public sealed record CurrentTree(
    IReadOnlyList<Category> Categories,
    IReadOnlyList<AttributeDefinition> Attributes,
    IReadOnlyList<AttributeValue> Values,
    IReadOnlyList<CategoryAttribute> Links);
