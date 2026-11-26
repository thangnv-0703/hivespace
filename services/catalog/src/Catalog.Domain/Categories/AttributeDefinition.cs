namespace Catalog.Domain.Categories;

/// <summary>An attribute a seller fills for a product (FR-CAT-019). Global: one name per source key.</summary>
public sealed class AttributeDefinition
{
    private AttributeDefinition()
    {
        Name = new LocalizedName(string.Empty, string.Empty);
    }

    public AttributeDefinition(int id, LocalizedName name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; private set; }

    public LocalizedName Name { get; private set; }

    public void Rename(LocalizedName name) => Name = name;
}
