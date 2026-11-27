namespace Catalog.Domain.Categories;

/// <summary>
/// An attribute linked to a category: its position in the category and whether it is required there (FR-CAT-019). The list
/// settings live on the <see cref="AttributeDefinition"/> (D-11).
/// </summary>
public sealed class CategoryAttribute
{
    private CategoryAttribute()
    {
    }

    public CategoryAttribute(int categoryId, int attributeId, int position, bool isRequired)
    {
        CategoryId = categoryId;
        AttributeId = attributeId;
        Position = position;
        IsRequired = isRequired;
    }

    public int CategoryId { get; private set; }

    public int AttributeId { get; private set; }

    public int Position { get; private set; }

    public bool IsRequired { get; private set; }

    public void Reposition(int position) => Position = position;

    /// <summary>Required → optional, the only loosening of a link (BR-014-6).</summary>
    public void MakeOptional() => IsRequired = false;
}
