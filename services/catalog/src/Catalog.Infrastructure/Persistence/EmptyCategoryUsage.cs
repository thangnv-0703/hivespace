using Catalog.Application.Abstractions;

namespace Catalog.Infrastructure.Persistence;

/// <summary>No products exist yet, so no category has any. FEAT-012 replaces this with a query on its products table.</summary>
public sealed class EmptyCategoryUsage : ICategoryUsage
{
    public Task<IReadOnlySet<int>> CategoriesWithProductsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
}
