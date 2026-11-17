namespace Catalog.Domain.Categories;

/// <summary>
/// A node in the platform category tree with vi and en names (FR-CAT-003, FR-LOC-007).
/// Walking skeleton: flat, one seeded row. FEAT-014 adds the tree, attributes and the seed command.
/// </summary>
public sealed class Category
{
    private Category()
    {
        Slug = string.Empty;
        NameVi = string.Empty;
        NameEn = string.Empty;
    }

    public Category(Guid id, string slug, string nameVi, string nameEn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameVi);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        Id = id;
        Slug = slug;
        NameVi = nameVi;
        NameEn = nameEn;
    }

    public Guid Id { get; private set; }

    public string Slug { get; private set; }

    public string NameVi { get; private set; }

    public string NameEn { get; private set; }
}
