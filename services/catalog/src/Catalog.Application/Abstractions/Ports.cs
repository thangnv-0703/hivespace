using Catalog.Domain.Categories;

namespace Catalog.Application.Abstractions;

/// <summary>An attribute of a leaf with its definition and its category's ordered value list.</summary>
public sealed record AttributeData(CategoryAttribute Link, AttributeDefinition Definition, IReadOnlyList<AttributeValue> Values);

/// <summary>A category with its path from the root (parent last) and, for a leaf, its attributes in file order.</summary>
public sealed record CategoryDetailData(Category Category, IReadOnlyList<Category> Path, IReadOnlyList<AttributeData> Attributes);

/// <summary>Read access to the category tree. Implemented in Infrastructure (EF Core).</summary>
public interface ICategoryReadStore
{
    /// <summary>The children of <paramref name="parentId"/> (the roots when null) ordered by position; <c>null</c> when the parent is not a category.</summary>
    Task<IReadOnlyList<Category>?> ListChildrenAsync(int? parentId, CancellationToken cancellationToken);

    /// <summary><c>null</c> when the category does not exist.</summary>
    Task<CategoryDetailData?> GetDetailAsync(int id, CancellationToken cancellationToken);
}

/// <summary>Port to the Inventory service (gRPC, architecture §5.3).</summary>
public interface IInventoryClient
{
    /// <summary>Skeleton probe: proves the Catalog → Inventory gRPC call. Replaced by real availability in FEAT-015.</summary>
    Task<string> ProbeAsync(CancellationToken cancellationToken);
}

/// <summary>Writes an event to the outbox in the caller's unit of work (ADR-0002).</summary>
public interface IEventOutbox
{
    Task<Guid> WriteAsync<T>(string eventType, int version, string correlationId, T data, CancellationToken cancellationToken);

    /// <summary>Adds the outbox row without saving, so it commits with the caller's next save (a category load, ADR-0002).</summary>
    Guid Stage<T>(string eventType, int version, string correlationId, T data);
}

/// <summary>What the file reader found: the raw tree, or the problems that stopped it (design §3.5, first block).</summary>
public sealed record SourceReadResult(RawCategoryTree? Tree, IReadOnlyList<Problem> Problems);

/// <summary>Reads the load directory (categories.json and category-attributes/*.json).</summary>
public interface ICategoryTreeSourceReader
{
    Task<SourceReadResult> ReadAsync(string sourcePath, CancellationToken cancellationToken);
}

/// <summary>The stored tree inside one load transaction, which already holds the tree lock.</summary>
public interface ICategoryTreeLoad
{
    Task<CurrentTree> LoadCurrentAsync(CancellationToken cancellationToken);

    Task ApplyAsync(ChangeSet changes, CancellationToken cancellationToken);
}

/// <summary>How a load step ends: its result, and whether the transaction commits or rolls back.</summary>
public readonly record struct LoadStep<T>(T Result, bool Commit);

/// <summary>Runs a load as one transaction under the tree's advisory lock (D-7). Retries start the work again from scratch.</summary>
public interface ICategoryTreeStore
{
    Task<T> RunLoadAsync<T>(Func<ICategoryTreeLoad, Task<LoadStep<T>>> work, CancellationToken cancellationToken);
}

/// <summary>Which categories have products. FEAT-014 answers with none; FEAT-012 replaces it with a query on products.</summary>
public interface ICategoryUsage
{
    Task<IReadOnlySet<int>> CategoriesWithProductsAsync(CancellationToken cancellationToken);
}
