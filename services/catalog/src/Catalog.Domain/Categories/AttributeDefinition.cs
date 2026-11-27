namespace Catalog.Domain.Categories;

/// <summary>
/// An attribute a seller fills for a product (FR-CAT-019): its name and its list settings, defined once and shared by every
/// category that links it (D-11). For a list, its values are the <see cref="AttributeValue"/> rows ordered by position.
/// </summary>
public sealed class AttributeDefinition
{
    private AttributeDefinition()
    {
        Name = new LocalizedName(string.Empty, string.Empty);
    }

    public AttributeDefinition(int id, LocalizedName name, AttributeKind kind, bool allowsMultiple, int maxValues, bool allowsCustomValue)
    {
        Id = id;
        Name = name;
        Kind = kind;
        AllowsMultiple = allowsMultiple;
        MaxValues = maxValues;
        AllowsCustomValue = allowsCustomValue;
    }

    public int Id { get; private set; }

    public LocalizedName Name { get; private set; }

    public AttributeKind Kind { get; private set; }

    public bool AllowsMultiple { get; private set; }

    /// <summary>1 for single choice and text, at least 2 for multiple choice.</summary>
    public int MaxValues { get; private set; }

    /// <summary>An open list: a seller may type their own text besides picking a value (FR-CAT-022). Always false for text.</summary>
    public bool AllowsCustomValue { get; private set; }

    public void Rename(LocalizedName name) => Name = name;

    /// <summary>Loosens the list settings (BR-014-6). Callers have checked that each one is a loosening.</summary>
    public void Loosen(bool allowsMultiple, int maxValues, bool allowsCustomValue)
    {
        AllowsMultiple = allowsMultiple;
        MaxValues = maxValues;
        AllowsCustomValue = allowsCustomValue;
    }
}
