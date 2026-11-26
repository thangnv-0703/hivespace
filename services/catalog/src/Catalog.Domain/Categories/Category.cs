namespace Catalog.Domain.Categories;

/// <summary>
/// A node in the platform category tree (FR-CAT-003). The id is the source (Tiki) key, never generated (D-9).
/// </summary>
public sealed class Category
{
    private Category()
    {
        Name = new LocalizedName(string.Empty, string.Empty);
    }

    public Category(int id, int? parentId, int position, LocalizedName name, bool isLeaf, string? imageUrl)
    {
        Id = id;
        ParentId = parentId;
        Position = position;
        Name = name;
        IsLeaf = isLeaf;
        SetImage(imageUrl);
    }

    public int Id { get; private set; }

    public int? ParentId { get; private set; }

    public int Position { get; private set; }

    public LocalizedName Name { get; private set; }

    /// <summary>True when the category has no children in the loaded tree (BR-014-2).</summary>
    public bool IsLeaf { get; private set; }

    /// <summary>Link to the category image; only top-level categories have one (FR-CAT-021, BR-014-13).</summary>
    public string? ImageUrl { get; private set; }

    public void Rename(LocalizedName name) => Name = name;

    public void Reposition(int position) => Position = position;

    public void SetLeaf(bool isLeaf) => IsLeaf = isLeaf;

    /// <summary>Sets or clears the image. A category below the top level never has one.</summary>
    public void SetImage(string? imageUrl) => ImageUrl = ParentId is null ? imageUrl : null;
}
