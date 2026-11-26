using Catalog.Application.Abstractions;
using Catalog.Domain.Categories;

namespace Catalog.Application.Features.Categories;

public static class CategoryTreeEvents
{
    public const string Changed = "CategoryTreeChanged";
}

/// <summary>Payload of <c>CategoryTreeChanged/v1</c>: only that the tree changed (design D-6).</summary>
public sealed record CategoryTreeChangedData(DateTimeOffset ChangedAt);

public enum LoadOutcome
{
    Applied,
    Unchanged,
    Refused,
}

public sealed record LoadReport(LoadOutcome Outcome, Guid LoadId, string SourcePath, LoadCounts Counts, IReadOnlyList<Problem> Problems);

/// <summary>
/// The operator's category load (UC-020): read and clean the file, compare it with the stored tree, then apply every
/// change and queue <c>CategoryTreeChanged</c> in one transaction, or change nothing and list every problem (FR-CAT-019).
/// </summary>
public sealed class LoadCategoryTreeHandler(
    ICategoryTreeSourceReader reader,
    ICategoryTreeStore store,
    ICategoryUsage usage,
    IEventOutbox outbox,
    TimeProvider time)
{
    public async Task<LoadReport> HandleAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var loadId = Guid.CreateVersion7();
        LoadReport Refused(IReadOnlyList<Problem> problems) => new(LoadOutcome.Refused, loadId, sourcePath, LoadCounts.Empty, problems);

        var read = await reader.ReadAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        if (read.Tree is null || read.Problems.Count > 0)
        {
            return Refused(read.Problems);
        }

        var built = CategoryTreeSource.Build(read.Tree);
        if (built.Source is null)
        {
            return Refused(built.Problems);
        }

        var source = built.Source;
        return await store.RunLoadAsync(
            async load =>
            {
                var current = await load.LoadCurrentAsync(cancellationToken).ConfigureAwait(false);
                var used = await usage.CategoriesWithProductsAsync(cancellationToken).ConfigureAwait(false);
                var diff = CategoryTreeDiff.Compute(current, source, used);
                if (diff.IsRefused)
                {
                    return new LoadStep<LoadReport>(Refused(diff.Problems), false);
                }

                if (diff.Changes!.IsEmpty)
                {
                    return new LoadStep<LoadReport>(new LoadReport(LoadOutcome.Unchanged, loadId, sourcePath, diff.Counts, []), false);
                }

                await load.ApplyAsync(diff.Changes, cancellationToken).ConfigureAwait(false);
                outbox.Stage(CategoryTreeEvents.Changed, 1, loadId.ToString(), new CategoryTreeChangedData(time.GetUtcNow()));
                return new LoadStep<LoadReport>(new LoadReport(LoadOutcome.Applied, loadId, sourcePath, diff.Counts, []), true);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
