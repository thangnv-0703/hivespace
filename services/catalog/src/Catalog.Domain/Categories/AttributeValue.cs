namespace Catalog.Domain.Categories;

/// <summary>One choice of a list attribute. Global per source key; each category links an ordered subset.</summary>
public sealed class AttributeValue
{
    private AttributeValue()
    {
        Name = new LocalizedName(string.Empty, string.Empty);
    }

    public AttributeValue(int id, int attributeId, LocalizedName name)
    {
        Id = id;
        AttributeId = attributeId;
        Name = name;
    }

    public int Id { get; private set; }

    public int AttributeId { get; private set; }

    public LocalizedName Name { get; private set; }

    public void Rename(LocalizedName name) => Name = name;
}
