namespace Catalog.Domain.Categories;

/// <summary>How a category-attribute link takes its answer: pick from a list, or type free text (BR-014-8).</summary>
public enum AttributeKind
{
    List,
    Text,
}

/// <summary>
/// An attribute attached to a category: whether it is required, whether it is a list (single or multiple) or free
/// text, and the ordered value ids of this category's list (FR-CAT-019, FR-CAT-020).
/// </summary>
public sealed class CategoryAttribute
{
    private CategoryAttribute()
    {
        ValueIds = [];
    }

    public CategoryAttribute(
        int categoryId, int attributeId, int position, bool isRequired, AttributeKind kind, bool allowsMultiple, int maxValues, IEnumerable<int> valueIds)
    {
        CategoryId = categoryId;
        AttributeId = attributeId;
        Position = position;
        IsRequired = isRequired;
        Kind = kind;
        AllowsMultiple = allowsMultiple;
        MaxValues = maxValues;
        ValueIds = [.. valueIds];
    }

    public int CategoryId { get; private set; }

    public int AttributeId { get; private set; }

    public int Position { get; private set; }

    public bool IsRequired { get; private set; }

    public AttributeKind Kind { get; private set; }

    public bool AllowsMultiple { get; private set; }

    /// <summary>1 for single choice and text, at least 2 for multiple choice.</summary>
    public int MaxValues { get; private set; }

    /// <summary>Value ids in display order. Empty exactly when <see cref="Kind"/> is text.</summary>
    public List<int> ValueIds { get; private set; }

    public void Reposition(int position) => Position = position;

    /// <summary>Loosens the rules: optional, or a higher maximum (BR-014-6). Callers have checked it is a loosening.</summary>
    public void Loosen(bool isRequired, bool allowsMultiple, int maxValues)
    {
        IsRequired = isRequired;
        AllowsMultiple = allowsMultiple;
        MaxValues = maxValues;
    }

    /// <summary>Replaces the value list with the file's order, which contains every stored id plus any new ones.</summary>
    public void SetValueIds(IEnumerable<int> valueIds) => ValueIds = [.. valueIds];
}
