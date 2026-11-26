namespace Catalog.Application.Abstractions;

/// <summary>Which categories have products. FEAT-014 answers with none; FEAT-012 replaces it with a query on products.</summary>
public interface ICategoryUsage
{
    Task<IReadOnlySet<int>> CategoriesWithProductsAsync(CancellationToken cancellationToken);
}
