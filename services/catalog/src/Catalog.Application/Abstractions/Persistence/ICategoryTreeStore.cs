namespace Catalog.Application.Abstractions;

/// <summary>How a load step ends: its result, and whether the transaction commits or rolls back.</summary>
public readonly record struct LoadStep<T>(T Result, bool Commit);

/// <summary>Runs a load as one transaction under the tree's advisory lock (D-7). Retries start the work again from scratch.</summary>
public interface ICategoryTreeStore
{
    Task<T> RunLoadAsync<T>(Func<ICategoryTreeLoad, Task<LoadStep<T>>> work, CancellationToken cancellationToken);
}
