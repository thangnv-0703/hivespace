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
        var curCats = current.Categories.ToDictionary(c => c.Id);
        var curLinks = current.Links.ToDictionary(l => (l.CategoryId, l.AttributeId));
        var srcCats = source.Categories.ToDictionary(c => c.Id);
        var srcLinks = source.Links.ToDictionary(l => (l.CategoryId, l.AttributeId));

        var curAttrs = current.Attributes.ToDictionary(a => a.Id);
        var curValueIds = current.Values.GroupBy(v => v.AttributeId).ToDictionary(g => g.Key, g => g.OrderBy(v => v.Position).Select(v => v.Id).ToList());
        var problems = FindProblems(curCats, curLinks, curAttrs, curValueIds, srcCats, srcLinks, categoriesWithProducts);
        return problems.Count > 0
            ? new TreeDiffResult(problems, null, LoadCounts.Empty)
            : Build(current, source, curCats, curLinks);
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static List<Problem> FindProblems(
        Dictionary<int, Category> curCats, Dictionary<(int, int), CategoryAttribute> curLinks,
        Dictionary<int, AttributeDefinition> curAttrs, Dictionary<int, List<int>> curValueIds,
        Dictionary<int, SourceCategory> srcCats, Dictionary<(int, int), SourceLink> srcLinks, IReadOnlySet<int> used)
    {
        var problems = new List<Problem>();
        foreach (var c in curCats.Values)
        {
            if (!srcCats.TryGetValue(c.Id, out var s))
            {
                problems.Add(new Problem("category.removed", "category", Id(c.Id), null, $"\"{c.Name.Vi}\" is stored but not in the file"));
            }
            else if (s.ParentId != c.ParentId)
            {
                problems.Add(new Problem("category.moved", "category", Id(c.Id), null, $"\"{c.Name.Vi}\": parent {c.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "none"} → {s.ParentId?.ToString(CultureInfo.InvariantCulture) ?? "none"}"));
            }
        }

        foreach (var s in srcCats.Values.Where(s => !curCats.ContainsKey(s.Id) && s.ParentId is { } p && used.Contains(p)))
        {
            problems.Add(new Problem("category.child_under_used_leaf", "category", Id(s.Id), null, $"parent {s.ParentId} has products and cannot get a child"));
        }

        foreach (var cur in curLinks.Values)
        {
            if (!srcLinks.TryGetValue((cur.CategoryId, cur.AttributeId), out var src))
            {
                problems.Add(new Problem("attribute.removed", "attribute", Id(cur.AttributeId), cur.CategoryId, "the link is stored but not in the file"));
                continue;
            }

            // Transitional (T10): the list settings are now stored on the attribute; T11 moves these checks to attribute level.
            var attr = curAttrs[cur.AttributeId];
            if (attr.Kind != src.Kind)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.AttributeId), cur.CategoryId, attr.Kind == AttributeKind.List ? "list → free text" : "free text → list"));
                continue;
            }

            if (!cur.IsRequired && src.IsRequired)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.AttributeId), cur.CategoryId, "optional → required"));
            }

            if (attr.AllowsMultiple && !src.AllowsMultiple)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.AttributeId), cur.CategoryId, "multiple → single"));
            }
            else if (attr.AllowsMultiple && src.MaxValues < attr.MaxValues)
            {
                problems.Add(new Problem("attribute.tightened", "attribute", Id(cur.AttributeId), cur.CategoryId, $"maximum {attr.MaxValues} → {src.MaxValues}"));
            }

            foreach (var valueId in curValueIds.GetValueOrDefault(cur.AttributeId, []).Except(src.ValueIds))
            {
                problems.Add(new Problem("value.removed", "value", Id(valueId), cur.CategoryId, $"the value of attribute {cur.AttributeId} is stored but not in the file's list"));
            }
        }

        foreach (var src in srcLinks.Values.Where(l => l.IsRequired && used.Contains(l.CategoryId) && !curLinks.ContainsKey((l.CategoryId, l.AttributeId))))
        {
            problems.Add(new Problem("attribute.required_on_used_category", "attribute", Id(src.AttributeId), src.CategoryId, "a new required attribute on a category with products"));
        }

        return problems;
    }

    private static TreeDiffResult Build(
        CurrentTree current, CategoryTreeSource source, Dictionary<int, Category> curCats, Dictionary<(int, int), CategoryAttribute> curLinks)
    {
        var curAttrs = current.Attributes.ToDictionary(a => a.Id);
        var curValues = current.Values.ToDictionary(v => v.Id);
        var curValueIds = current.Values.GroupBy(v => v.AttributeId).ToDictionary(g => g.Key, g => g.OrderBy(v => v.Position).Select(v => v.Id).ToList());

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

        // Transitional (T10): settings come from the attribute's first link and a value's position from the order in which the
        // links list it; T11 builds attributes and values from attributes.json.
        var firstLink = source.Links.GroupBy(l => l.AttributeId).ToDictionary(g => g.Key, g => g.First());
        var valueOrder = source.Links.GroupBy(l => l.AttributeId)
            .ToDictionary(g => g.Key, g => g.SelectMany(l => l.ValueIds).Distinct().ToList());
        var newAttributes = new List<AttributeDefinition>();
        var attributeRenames = new List<NameChange>();
        foreach (var a in source.Attributes)
        {
            if (!curAttrs.TryGetValue(a.Id, out var cur))
            {
                var l = firstLink[a.Id];
                newAttributes.Add(new AttributeDefinition(a.Id, a.Name, l.Kind, l.AllowsMultiple, l.MaxValues, false));
            }
            else if (cur.Name != a.Name)
            {
                attributeRenames.Add(new NameChange(a.Id, a.Name));
            }
        }

        var newValues = new List<AttributeValue>();
        var valueRenames = new List<NameChange>();
        foreach (var v in source.Values)
        {
            if (!curValues.TryGetValue(v.Id, out var cur))
            {
                newValues.Add(new AttributeValue(v.Id, v.AttributeId, valueOrder[v.AttributeId].IndexOf(v.Id), v.Name));
            }
            else if (cur.Name != v.Name)
            {
                valueRenames.Add(new NameChange(v.Id, v.Name));
            }
        }

        var renamedAttributes = attributeRenames.Select(r => r.Id).ToHashSet();
        var renamedValues = valueRenames.Select(r => r.Id).ToHashSet();
        var newLinks = new List<CategoryAttribute>();
        var linkUpdates = new List<LinkUpdate>();
        int linksAdded = 0, linksLoosened = 0, linksReordered = 0, linksUnchanged = 0;
        int pairsAdded = 0, pairsReordered = 0, pairsUnchanged = 0, pairsTotal = 0;

        foreach (var l in source.Links)
        {
            pairsTotal += l.ValueIds.Count;
            if (!curLinks.TryGetValue((l.CategoryId, l.AttributeId), out var cur))
            {
                newLinks.Add(new CategoryAttribute(l.CategoryId, l.AttributeId, l.Position, l.IsRequired));
                linksAdded++;
                pairsAdded += l.ValueIds.Count;
                continue;
            }

            var attr = curAttrs[l.AttributeId];
            var stored = curValueIds.GetValueOrDefault(l.AttributeId, []);
            var loosened = (cur.IsRequired && !l.IsRequired) || (!attr.AllowsMultiple && l.AllowsMultiple) || l.MaxValues > attr.MaxValues;
            var reordered = cur.Position != l.Position;
            var valuesChanged = !stored.SequenceEqual(l.ValueIds);
            if (loosened || reordered || valuesChanged)
            {
                linkUpdates.Add(new LinkUpdate(l.CategoryId, l.AttributeId, l.Position, l.IsRequired, l.AllowsMultiple, l.MaxValues, l.ValueIds));
            }

            linksLoosened += loosened ? 1 : 0;
            linksReordered += reordered ? 1 : 0;
            linksUnchanged += !loosened && !reordered && !renamedAttributes.Contains(l.AttributeId) ? 1 : 0;

            for (var i = 0; i < l.ValueIds.Count; i++)
            {
                var index = stored.IndexOf(l.ValueIds[i]);
                if (index < 0)
                {
                    pairsAdded++;
                }
                else if (index != i)
                {
                    pairsReordered++;
                }
                else if (!renamedValues.Contains(l.ValueIds[i]))
                {
                    pairsUnchanged++;
                }
            }
        }

        var changes = new ChangeSet(
            [.. newCategories.OrderBy(c => c.Depth).Select(c => c.Category)], newAttributes, newValues, newLinks, categoryRenames,
            attributeRenames, valueRenames, repositions, leafChanges, imageChanges, linkUpdates);
        var counts = new LoadCounts(
            new CountRow(categoriesAdded, categoriesRenamed, 0, categoriesReordered, categoriesUnchanged, source.Categories.Count),
            new CountRow(leavesAdded, 0, 0, 0, leavesTotal - leavesAdded, leavesTotal),
            new CountRow(linksAdded, attributeRenames.Count, linksLoosened, linksReordered, linksUnchanged, source.Links.Count),
            new CountRow(pairsAdded, valueRenames.Count, 0, pairsReordered, pairsUnchanged, pairsTotal),
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
