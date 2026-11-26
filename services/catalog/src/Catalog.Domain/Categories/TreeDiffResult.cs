namespace Catalog.Domain.Categories;

public sealed record LinkUpdate(
    int CategoryId, int AttributeId, int Position, bool IsRequired, bool AllowsMultiple, int MaxValues, IReadOnlyList<int> ValueIds);

public sealed record NameChange(int Id, LocalizedName Name);

/// <summary>Everything a load must write: new rows first, then changes to stored rows.</summary>
public sealed record ChangeSet(
    IReadOnlyList<Category> NewCategories,
    IReadOnlyList<AttributeDefinition> NewAttributes,
    IReadOnlyList<AttributeValue> NewValues,
    IReadOnlyList<CategoryAttribute> NewLinks,
    IReadOnlyList<NameChange> CategoryRenames,
    IReadOnlyList<NameChange> AttributeRenames,
    IReadOnlyList<NameChange> ValueRenames,
    IReadOnlyList<(int Id, int Position)> CategoryRepositions,
    IReadOnlyList<(int Id, bool IsLeaf)> LeafChanges,
    IReadOnlyList<(int Id, string? ImageUrl)> ImageChanges,
    IReadOnlyList<LinkUpdate> LinkUpdates)
{
    public bool IsEmpty =>
        NewCategories.Count + NewAttributes.Count + NewValues.Count + NewLinks.Count + CategoryRenames.Count + AttributeRenames.Count
        + ValueRenames.Count + CategoryRepositions.Count + LeafChanges.Count + ImageChanges.Count + LinkUpdates.Count == 0;
}

/// <summary>One row of the load report: how many items of a kind were added, renamed, loosened, reordered or left alone.</summary>
public sealed record CountRow(int Added, int Renamed, int Loosened, int Reordered, int Unchanged, int Total)
{
    public static CountRow Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

public sealed record ImageCounts(int Added, int Changed, int Removed, int Unchanged);

public sealed record LoadCounts(CountRow Categories, CountRow Leaves, CountRow Attributes, CountRow ListValues, ImageCounts Images)
{
    public static LoadCounts Empty { get; } = new(CountRow.Empty, CountRow.Empty, CountRow.Empty, CountRow.Empty, new ImageCounts(0, 0, 0, 0));
}

/// <summary>Either every problem (the load is refused) or the change set with its counts.</summary>
public sealed record TreeDiffResult(IReadOnlyList<Problem> Problems, ChangeSet? Changes, LoadCounts Counts)
{
    public bool IsRefused => Problems.Count > 0;
}
