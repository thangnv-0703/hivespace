using System.Globalization;

namespace Catalog.Domain.Categories;

/// <summary>
/// Compares the stored tree with the file and either lists every problem (BR-014-7) or returns what to write (BR-014-6).
/// Pure: all of AC-014-1.x and 2.x are unit-testable.
/// </summary>
public static class CategoryTreeDiff
{
    public static TreeDiffResult Compute(CurrentTree current, CategoryTreeSource source, IReadOnlySet<int> categoriesWithProducts)
    {
        var problems = FindProblems(current, source, categoriesWithProducts);
        return problems.Count > 0 ? new TreeDiffResult(problems, null, LoadCounts.Empty) : Build(current, source);
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static string Id(int? id) => id?.ToString(CultureInfo.InvariantCulture) ?? "none";

    private static List<Problem> FindProblems(CurrentTree current, CategoryTreeSource source, IReadOnlySet<int> used)
    {
        var problems = new List<Problem>();
        var srcCats = source.Categories.ToDictionary(c => c.Id);
        foreach (var c in current.Categories)
        {
            if (!srcCats.TryGetValue(c.Id, out var s))
            {
                problems.Add(new Problem("category.removed", "category", Id(c.Id), null, $"\"{c.Name.Vi}\" is stored but not in the file"));
            }
            else if (s.ParentId != c.ParentId)
            {
                problems.Add(new Problem("category.moved", "category", Id(c.Id), null, $"\"{c.Name.Vi}\": parent {Id(c.ParentId)} → {Id(s.ParentId)}"));
            }
        }

        var curCatIds = current.Categories.Select(c => c.Id).ToHashSet();
        foreach (var s in source.Categories.Where(s => !curCatIds.Contains(s.Id) && s.ParentId is { } p && used.Contains(p)))
        {
            problems.Add(new Problem("category.child_under_used_leaf", "category", Id(s.Id), null, $"parent {s.ParentId} has products and cannot get a child"));
        }

        // Attribute level: the list settings and the values are defined once, on the attribute (D-11).
        var srcAttrs = source.Attributes.ToDictionary(a => a.Id);
        var curValues = current.Values.ToLookup(v => v.AttributeId);
        foreach (var cur in current.Attributes)
        {
            if (!srcAttrs.TryGetValue(cur.Id, out var src))
            {
                problems.Add(new Problem("attribute.removed", "attribute", Id(cur.Id), null, $"\"{cur.Name.Vi}\" is stored but not in attributes.json"));
                continue;
            }

            if (cur.Kind != src.Kind)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.Id), null, cur.Kind == AttributeKind.List ? "list → free text" : "free text → list"));
                continue;
            }

            if (cur.AllowsMultiple && !src.AllowsMultiple)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.Id), null, "multiple → single"));
            }
            else if (cur.AllowsMultiple && src.MaxValues < cur.MaxValues)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.Id), null, $"maximum {cur.MaxValues} → {src.MaxValues}"));
            }

            if (cur.AllowsCustomValue && !src.AllowsCustomValue)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.Id), null, "accepts other values → does not"));
            }

            foreach (var value in curValues[cur.Id].Where(v => !src.ValueIds.Contains(v.Id)))
            {
                problems.Add(new Problem("value.removed", "value", Id(value.Id), null, $"the value of attribute {cur.Id} is stored but not in its list in attributes.json"));
            }
        }

        // Link level: whether the attribute is required is set per category.
        var srcLinks = source.Links.ToDictionary(l => (l.CategoryId, l.AttributeId));
        foreach (var cur in current.Links)
        {
            if (!srcLinks.TryGetValue((cur.CategoryId, cur.AttributeId), out var src))
            {
                problems.Add(new Problem("attribute.removed", "attribute", Id(cur.AttributeId), cur.CategoryId, "the link is stored but not in the file"));
            }
            else if (!cur.IsRequired && src.IsRequired)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.AttributeId), cur.CategoryId, "optional → required"));
            }
        }

        FindUsedLeafProblems(current, source, used, problems);
        return problems;
    }

    /// <summary>
    /// A category with products keeps what its products were filled against: its effective attributes in the file must not
    /// gain a required attribute (AC-014-2.7), and, when they come from another category than before, must keep every
    /// attribute it had (AC-014-2.8). A superset passes (AC-014-2.9).
    /// </summary>
    private static void FindUsedLeafProblems(CurrentTree current, CategoryTreeSource source, IReadOnlySet<int> used, List<Problem> problems)
    {
        if (used.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<int, int?> curParents = current.Categories.ToDictionary(c => c.Id, c => c.ParentId);
        IReadOnlyDictionary<int, int?> srcParents = source.Categories.ToDictionary(c => c.Id, c => c.ParentId);
        IReadOnlyDictionary<int, IReadOnlyList<CategoryAttribute>> curLinks =
            current.Links.GroupBy(l => l.CategoryId).ToDictionary(g => g.Key, g => (IReadOnlyList<CategoryAttribute>)[.. g]);
        IReadOnlyDictionary<int, IReadOnlyList<SourceLink>> srcLinks =
            source.Links.GroupBy(l => l.CategoryId).ToDictionary(g => g.Key, g => (IReadOnlyList<SourceLink>)[.. g]);

        foreach (var leaf in used.Order().Where(id => curParents.ContainsKey(id) && srcParents.ContainsKey(id)))
        {
            var cur = EffectiveAttributes.For(leaf, curParents, curLinks);
            var src = EffectiveAttributes.For(leaf, srcParents, srcLinks);
            var had = cur.Links.Select(l => l.AttributeId).ToHashSet();
            var has = src.Links.Select(l => l.AttributeId).ToHashSet();

            foreach (var link in src.Links.Where(l => l.IsRequired && !had.Contains(l.AttributeId)))
            {
                var via = src.FromCategoryId == leaf ? string.Empty : $" (inherited from category {src.FromCategoryId})";
                problems.Add(new Problem("attribute.required_on_used_category", "attribute", Id(link.AttributeId), leaf, $"a new required attribute on a category with products{via}"));
            }

            // With the same provider, a missing attribute is already reported as attribute.removed.
            if (cur.FromCategoryId != src.FromCategoryId)
            {
                foreach (var link in cur.Links.Where(l => !has.Contains(l.AttributeId)))
                {
                    problems.Add(new Problem(
                        "attribute.inherited_dropped", "attribute", Id(link.AttributeId), leaf,
                        $"category {leaf} has products and its attributes would come from category {Id(src.FromCategoryId)} instead of {Id(cur.FromCategoryId)}, which leaves this attribute out"));
                }
            }
        }
    }

    private static TreeDiffResult Build(CurrentTree current, CategoryTreeSource source)
    {
        var curCats = current.Categories.ToDictionary(c => c.Id);
        var curAttrs = current.Attributes.ToDictionary(a => a.Id);
        var curValues = current.Values.ToDictionary(v => v.Id);
        var curLinks = current.Links.ToDictionary(l => (l.CategoryId, l.AttributeId));

        var newCategories = new List<(int Depth, Category Category)>();
        var categoryRenames = new List<NameChange>();
        var repositions = new List<(int, int)>();
        var leafChanges = new List<(int, bool)>();
        var imageChanges = new List<(int, string?)>();
        var srcById = source.Categories.ToDictionary(c => c.Id);
        int categoriesAdded = 0, categoriesRenamed = 0, categoriesReordered = 0, categoriesUnchanged = 0, leavesAdded = 0, leavesTotal = 0;
        int imagesAdded = 0, imagesChanged = 0, imagesRemoved = 0, imagesUnchanged = 0;

        foreach (var s in source.Categories)
        {
            if (s.IsLeaf)
            {
                leavesTotal++;
            }

            if (!curCats.TryGetValue(s.Id, out var cur))
            {
                newCategories.Add((Depth(s, srcById), new Category(s.Id, s.ParentId, s.Position, s.Name, s.IsLeaf, s.ImageUrl)));
                categoriesAdded++;
                leavesAdded += s.IsLeaf ? 1 : 0;
                imagesAdded += s.ImageUrl is null ? 0 : 1;
                continue;
            }

            var renamed = cur.Name != s.Name;
            var reordered = cur.Position != s.Position;
            var leafChanged = cur.IsLeaf != s.IsLeaf;
            var imageChanged = cur.ImageUrl != s.ImageUrl;
            if (renamed)
            {
                categoryRenames.Add(new NameChange(s.Id, s.Name));
                categoriesRenamed++;
            }

            if (reordered)
            {
                repositions.Add((s.Id, s.Position));
                categoriesReordered++;
            }

            if (leafChanged)
            {
                leafChanges.Add((s.Id, s.IsLeaf));
                leavesAdded += s.IsLeaf ? 1 : 0;
            }

            if (imageChanged)
            {
                imageChanges.Add((s.Id, s.ImageUrl));
                (imagesAdded, imagesChanged, imagesRemoved) = cur.ImageUrl is null
                    ? (imagesAdded + 1, imagesChanged, imagesRemoved)
                    : s.ImageUrl is null ? (imagesAdded, imagesChanged, imagesRemoved + 1) : (imagesAdded, imagesChanged + 1, imagesRemoved);
            }
            else if (s.ImageUrl is not null)
            {
                imagesUnchanged++;
            }

            if (!renamed && !reordered && !leafChanged && !imageChanged)
            {
                categoriesUnchanged++;
            }
        }

        // Attributes: single → multiple, a higher maximum, or a list that starts accepting other values (BR-014-6).
        var newAttributes = new List<AttributeDefinition>();
        var attributeRenames = new List<NameChange>();
        var attributeUpdates = new List<AttributeUpdate>();
        var attributesUnchanged = 0;
        foreach (var a in source.Attributes)
        {
            if (!curAttrs.TryGetValue(a.Id, out var cur))
            {
                newAttributes.Add(new AttributeDefinition(a.Id, a.Name, a.Kind, a.AllowsMultiple, a.MaxValues, a.AllowsCustomValue));
                continue;
            }

            var renamed = cur.Name != a.Name;
            var loosened = (!cur.AllowsMultiple && a.AllowsMultiple) || a.MaxValues > cur.MaxValues || (!cur.AllowsCustomValue && a.AllowsCustomValue);
            if (renamed)
            {
                attributeRenames.Add(new NameChange(a.Id, a.Name));
            }

            if (loosened)
            {
                attributeUpdates.Add(new AttributeUpdate(a.Id, a.AllowsMultiple, a.MaxValues, a.AllowsCustomValue));
            }

            attributesUnchanged += !renamed && !loosened ? 1 : 0;
        }

        // List values: new ones where the file puts them; kept ones take their file order (BR-014-3).
        var newValues = new List<AttributeValue>();
        var valueRenames = new List<NameChange>();
        var valueRepositions = new List<(int, int)>();
        var valuesUnchanged = 0;
        foreach (var v in source.Values)
        {
            if (!curValues.TryGetValue(v.Id, out var cur))
            {
                newValues.Add(new AttributeValue(v.Id, v.AttributeId, v.Position, v.Name));
                continue;
            }

            var renamed = cur.Name != v.Name;
            var reordered = cur.Position != v.Position;
            if (renamed)
            {
                valueRenames.Add(new NameChange(v.Id, v.Name));
            }

            if (reordered)
            {
                valueRepositions.Add((v.Id, v.Position));
            }

            valuesUnchanged += !renamed && !reordered ? 1 : 0;
        }

        // Links: a new position, or required → optional.
        var newLinks = new List<CategoryAttribute>();
        var linkUpdates = new List<LinkUpdate>();
        int linksLoosened = 0, linksReordered = 0, linksUnchanged = 0;
        foreach (var l in source.Links)
        {
            if (!curLinks.TryGetValue((l.CategoryId, l.AttributeId), out var cur))
            {
                newLinks.Add(new CategoryAttribute(l.CategoryId, l.AttributeId, l.Position, l.IsRequired));
                continue;
            }

            var loosened = cur.IsRequired && !l.IsRequired;
            var reordered = cur.Position != l.Position;
            if (loosened || reordered)
            {
                linkUpdates.Add(new LinkUpdate(l.CategoryId, l.AttributeId, l.Position, l.IsRequired));
            }

            linksLoosened += loosened ? 1 : 0;
            linksReordered += reordered ? 1 : 0;
            linksUnchanged += !loosened && !reordered ? 1 : 0;
        }

        var changes = new ChangeSet(
            [.. newCategories.OrderBy(c => c.Depth).Select(c => c.Category)], newAttributes, newValues, newLinks, categoryRenames,
            attributeRenames, valueRenames, repositions, leafChanges, imageChanges, attributeUpdates, linkUpdates, valueRepositions);
        var counts = new LoadCounts(
            new CountRow(categoriesAdded, categoriesRenamed, 0, categoriesReordered, categoriesUnchanged, source.Categories.Count),
            new CountRow(leavesAdded, 0, 0, 0, leavesTotal - leavesAdded, leavesTotal),
            new CountRow(newAttributes.Count, attributeRenames.Count, attributeUpdates.Count, 0, attributesUnchanged, source.Attributes.Count),
            new CountRow(newLinks.Count, 0, linksLoosened, linksReordered, linksUnchanged, source.Links.Count),
            new CountRow(newValues.Count, valueRenames.Count, 0, valueRepositions.Count, valuesUnchanged, source.Values.Count),
            new ImageCounts(imagesAdded, imagesChanged, imagesRemoved, imagesUnchanged));
        return new TreeDiffResult([], changes, counts);
    }

    private static int Depth(SourceCategory category, Dictionary<int, SourceCategory> byId)
    {
        var depth = 0;
        for (var parent = category.ParentId; parent is { } p && byId.TryGetValue(p, out var next); parent = next.ParentId)
        {
            depth++;
        }

        return depth;
    }
}
