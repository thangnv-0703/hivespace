namespace Catalog.Domain.Categories;

/// <summary>One choice of a list attribute, at its position in the attribute's list (D-11).</summary>
public sealed class AttributeValue
{
    private AttributeValue()
    {
        Name = new LocalizedName(string.Empty, string.Empty);
    }

    public AttributeValue(int id, int attributeId, int position, LocalizedName name)
    {
        Id = id;
        AttributeId = attributeId;
        Position = position;
        Name = name;
    }

    public int Id { get; private set; }

    public int AttributeId { get; private set; }

    /// <summary>Index within the attribute's list, in the file's order (BR-014-3).</summary>
    public int Position { get; private set; }

    public LocalizedName Name { get; private set; }

    public void Rename(LocalizedName name) => Name = name;

    public void Reposition(int position) => Position = position;
}
