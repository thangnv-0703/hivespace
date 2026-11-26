using Catalog.Domain.Categories;

namespace Catalog.Application.Abstractions;

/// <summary>The stored tree inside one load transaction, which already holds the tree lock.</summary>
public interface ICategoryTreeLoad
{
    Task<CurrentTree> LoadCurrentAsync(CancellationToken cancellationToken);

    Task ApplyAsync(ChangeSet changes, CancellationToken cancellationToken);
}
