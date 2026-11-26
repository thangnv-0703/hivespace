using Catalog.Domain.Categories;

namespace Catalog.Application.Abstractions;

/// <summary>What the file reader found: the raw tree, or the problems that stopped it (design §3.5, first block).</summary>
public sealed record SourceReadResult(RawCategoryTree? Tree, IReadOnlyList<Problem> Problems);

/// <summary>Reads the load directory (categories.json and category-attributes/*.json).</summary>
public interface ICategoryTreeSourceReader
{
    Task<SourceReadResult> ReadAsync(string sourcePath, CancellationToken cancellationToken);
}
