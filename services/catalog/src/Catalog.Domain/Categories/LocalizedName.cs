namespace Catalog.Domain.Categories;

/// <summary>A platform name in both languages (FR-LOC-007). Both parts are always present.</summary>
public sealed record LocalizedName(string Vi, string En);
