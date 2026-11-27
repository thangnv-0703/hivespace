using Catalog.Application.Abstractions;
using Catalog.Application.Features.Categories;
using Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// Runs a category load in one transaction under <see cref="CategoryTreeLock.Key"/>. The whole unit is wrapped in the
/// execution strategy, so a retry starts the work again on a clean change tracker.
/// </summary>
public sealed class EfCategoryTreeStore(CatalogDbContext db) : ICategoryTreeStore
{
    public Task<T> RunLoadAsync<T>(Func<ICategoryTreeLoad, Task<LoadStep<T>>> work, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(
            async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CategoryTreeLock.Key})", cancellationToken).ConfigureAwait(false);

                var step = await work(new Load(db)).ConfigureAwait(false);
                if (step.Commit)
                {
                    // Saves the load and the staged outbox row together, then commits (ADR-0002).
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return step.Result;
            });

    private sealed class Load(CatalogDbContext db) : ICategoryTreeLoad
    {
        private Dictionary<int, Category> _categories = [];
        private Dictionary<int, AttributeDefinition> _attributes = [];
        private Dictionary<int, AttributeValue> _values = [];
        private Dictionary<(int, int), CategoryAttribute> _links = [];

        public async Task<CurrentTree> LoadCurrentAsync(CancellationToken cancellationToken)
        {
            var categories = await db.Categories.ToListAsync(cancellationToken).ConfigureAwait(false);
            var attributes = await db.Attributes.ToListAsync(cancellationToken).ConfigureAwait(false);
            var values = await db.AttributeValues.ToListAsync(cancellationToken).ConfigureAwait(false);
            var links = await db.CategoryAttributes.ToListAsync(cancellationToken).ConfigureAwait(false);
            _categories = categories.ToDictionary(c => c.Id);
            _attributes = attributes.ToDictionary(a => a.Id);
            _values = values.ToDictionary(v => v.Id);
            _links = links.ToDictionary(l => (l.CategoryId, l.AttributeId));
            return new CurrentTree(categories, attributes, values, links);
        }

        public async Task ApplyAsync(ChangeSet changes, CancellationToken cancellationToken)
        {
            foreach (var r in changes.CategoryRenames)
            {
                _categories[r.Id].Rename(r.Name);
            }

            foreach (var r in changes.AttributeRenames)
            {
                _attributes[r.Id].Rename(r.Name);
            }

            foreach (var r in changes.ValueRenames)
            {
                _values[r.Id].Rename(r.Name);
            }

            foreach (var (id, position) in changes.CategoryRepositions)
            {
                _categories[id].Reposition(position);
            }

            foreach (var (id, isLeaf) in changes.LeafChanges)
            {
                _categories[id].SetLeaf(isLeaf);
            }

            foreach (var (id, imageUrl) in changes.ImageChanges)
            {
                _categories[id].SetImage(imageUrl);
            }

            foreach (var u in changes.LinkUpdates)
            {
                // Transitional (T10): list settings are stored on the attribute; T12 applies the amended change set.
                var link = _links[(u.CategoryId, u.AttributeId)];
                link.Reposition(u.Position);
                if (link.IsRequired && !u.IsRequired)
                {
                    link.MakeOptional();
                }

                var attribute = _attributes[u.AttributeId];
                attribute.Loosen(u.AllowsMultiple, u.MaxValues, attribute.AllowsCustomValue);
            }

            db.Attributes.AddRange(changes.NewAttributes);
            db.AttributeValues.AddRange(changes.NewValues);
            db.Categories.AddRange(changes.NewCategories);
            db.CategoryAttributes.AddRange(changes.NewLinks);

            // Written inside the open transaction; the staged outbox row is saved and everything committed in RunLoadAsync.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
