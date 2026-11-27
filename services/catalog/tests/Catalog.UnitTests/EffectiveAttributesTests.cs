using Catalog.Domain.Categories;
using Xunit;

namespace Catalog.UnitTests;

public sealed class EffectiveAttributesTests
{
    // Root 1 (links) → parent 2 (links) → leaf 3 (none) and leaf 4 (own links); leaf 6 under root 5, and nothing has links there.
    private static readonly Dictionary<int, int?> ParentOf = new() { [1] = null, [2] = 1, [3] = 2, [4] = 2, [5] = null, [6] = 5 };

    private static readonly Dictionary<int, IReadOnlyList<string>> Links = new()
    {
        [1] = ["root-a"],
        [2] = ["parent-a", "parent-b"],
        [4] = ["own-a"],
    };

    [Fact]
    public void Ac_4_7_a_leaf_without_links_uses_its_nearest_ancestor_with_links()
    {
        var effective = EffectiveAttributes.For(3, ParentOf, Links);

        Assert.Equal(2, effective.FromCategoryId);
        Assert.Equal(["parent-a", "parent-b"], effective.Links);
    }

    [Fact]
    public void Ac_4_8_a_leaf_with_links_uses_only_its_own()
    {
        var effective = EffectiveAttributes.For(4, ParentOf, Links);

        Assert.Equal(4, effective.FromCategoryId);
        Assert.Equal(["own-a"], effective.Links);
    }

    [Fact]
    public void Ac_4_4_a_leaf_with_no_links_above_it_has_none()
    {
        var effective = EffectiveAttributes.For(6, ParentOf, Links);

        Assert.Null(effective.FromCategoryId);
        Assert.Empty(effective.Links);
    }

    [Fact]
    public void An_empty_link_list_counts_as_no_links() =>
        Assert.Equal(2, EffectiveAttributes.For(3, ParentOf, new Dictionary<int, IReadOnlyList<string>>(Links) { [3] = [] }).FromCategoryId);
}
