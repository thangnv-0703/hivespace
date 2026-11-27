using Catalog.Domain.Categories;
using Xunit;
using static Catalog.UnitTests.CategoryTreeTestData;

namespace Catalog.UnitTests;

public sealed class CategoryTreeSourceTests
{
    private static string[] Codes(RawCategoryTree raw) => [.. CategoryTreeSource.Build(raw).Problems.Select(p => p.Code)];

    [Fact]
    public void Ids_are_the_file_keys_and_positions_follow_file_order()
    {
        var source = Build(Baseline());

        Assert.Equal([1, 10, 11, 2, 20], source.Categories.Select(c => c.Id));
        Assert.Equal([0, 0, 1, 1, 0], source.Categories.Select(c => c.Position));
        Assert.Equal([false, true, true, false, true], source.Categories.Select(c => c.IsLeaf));
    }

    [Fact]
    public void A_child_listed_before_its_parent_is_accepted()
    {
        var source = Build(Tree([C("10", "1", "Con"), C("1", null, "Gốc")]));

        Assert.Equal(1, source.Categories.Single(c => c.Id == 10).ParentId);
        Assert.False(source.Categories.Single(c => c.Id == 1).IsLeaf);
    }

    [Fact]
    public void Numeric_keys_written_as_strings_become_integers() =>
        Assert.Equal(2549, Build(Tree([C("2549", null, "A")])).Categories.Single().Id);

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2147483648")]
    [InlineData("1.5")]
    [InlineData("")]
    public void A_key_that_is_not_a_whole_number_in_range_is_invalid(string key) =>
        Assert.Equal(["category.key_invalid"], Codes(Tree([C(key, null, "A")])));

    [Fact]
    public void Missing_names_are_listed_with_kind_and_owner()
    {
        // A name is given once, in attributes.json (D-13), so an attribute's problem has no category and a value's names its attribute.
        var raw = Tree(
            [new RawCategory("1", null, new RawName("Gốc", null), null), C("10", "1", "Con")],
            [new RawAttributeDefinition("100", new RawName(null, "Brand"), "Dropdown", 1, false, [V("1000", "A")]), D("101", "Màu", "Dropdown", 1, new RawValue("1100", new RawName("Đen", "  ")))],
            Entry("10", L("100"), L("101")));

        var problems = CategoryTreeSource.Build(raw).Problems;

        Assert.Equal(["name.missing", "name.missing", "name.missing"], problems.Select(p => p.Code));
        Assert.Equal(["category", "attribute", "value"], problems.Select(p => p.Kind));
        Assert.Equal(["1", "100", "1100"], problems.Select(p => p.Key));
        Assert.Equal(["en is missing", "vi is missing", "en is missing"], problems.Select(p => p.Detail));
    }

    [Fact]
    public void Same_vi_and_en_names_are_fine() =>
        Assert.Empty(CategoryTreeSource.Build(Tree([C("1", null, "Gốc"), C("10", "1", "Con")], [D("100", "Brand", "Dropdown", 1, V("1000", "Samsung"))], Entry("10", L("100")))).Problems);

    [Fact]
    public void Names_longer_than_the_column_are_listed() =>
        Assert.Equal(["name.too_long", "name.too_long"], Codes(Tree([C("1", null, new string('a', 201))])));

    [Fact]
    public void Structure_problems_are_all_listed()
    {
        Assert.Equal(["category.duplicate_key"], Codes(Tree([C("1", null, "A"), C("1", null, "B")])));
        Assert.Equal(["category.parent_unknown"], Codes(Tree([C("1", "9", "A")])));
        Assert.Contains("category.cycle", Codes(Tree([C("1", "2", "A"), C("2", "1", "B"), C("3", null, "C")])));
        Assert.Equal(["attributes.category_unknown"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1)], Entry("5", L("100")))));
        Assert.Equal(["attributes.category_duplicate"], Codes(Tree([C("1", null, "A")], [], Entry("1"), Entry("1"))));
    }

    [Fact]
    public void Attribute_and_value_problems_are_all_listed()
    {
        Assert.Equal(["attribute.duplicate_in_category"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1)], Entry("1", L("100"), L("100")))));
        Assert.Equal(["attribute.invalid_selection"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Radio", 1, V("1", "a"))])));
        Assert.Equal(["attribute.invalid_selection"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Checkbox", 1, V("1", "a"))])));
        Assert.Equal(["value.duplicate_key"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1, V("1", "a"), V("1", "b"))])));
        Assert.Equal(["value.other_attribute"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1, V("1", "a")), D("101", "y", "Dropdown", 1, V("1", "a"))])));
        Assert.Equal(["attribute.key_invalid"], Codes(Tree([C("1", null, "A")], [D("x", "x", "Dropdown", 1)])));
        Assert.Equal(["value.key_invalid"], Codes(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1, V("0", "a"))])));
    }

    [Fact]
    public void Tac_10_an_attribute_defined_twice_is_a_duplicate_key()
    {
        var problem = Assert.Single(CategoryTreeSource.Build(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1), D("100", "x", "Dropdown", 1)])).Problems);

        Assert.Equal(("attribute.duplicate_key", "100", null), (problem.Code, problem.Key, problem.CategoryId));
    }

    [Fact]
    public void Tac_10_a_link_to_an_attribute_that_is_not_defined_is_unknown_and_names_the_category()
    {
        var problem = Assert.Single(CategoryTreeSource.Build(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1)], Entry("1", L("100"), L("999")))).Problems);

        Assert.Equal(("attributes.unknown", "999", 1), (problem.Code, problem.Key, problem.CategoryId));
    }

    [Fact]
    public void A_link_to_a_definition_with_a_problem_is_not_also_unknown() =>
        Assert.Equal(["name.missing", "name.missing"], Codes(Tree([C("1", null, "A")], [new RawAttributeDefinition("100", new RawName(null, null), "Dropdown", 1, false, [])], Entry("1", L("100")))));

    [Fact]
    public void An_attribute_linked_to_no_category_is_kept()
    {
        var source = Build(Tree([C("1", null, "A")], [D("100", "x", "Dropdown", 1, V("1", "a"))]));

        Assert.Equal(100, Assert.Single(source.Attributes).Id);
        Assert.Empty(source.Links);
    }

    [Fact]
    public void Links_take_their_position_and_required_flag_from_the_category_entry()
    {
        var links = Build(Baseline()).Links;

        Assert.Equal([(10, 100, 0, true), (10, 101, 1, false), (20, 102, 0, false)], links.Select(l => (l.CategoryId, l.AttributeId, l.Position, l.IsRequired)));
    }

    [Fact]
    public void Two_values_equal_after_cleaning_and_ignoring_case_are_a_problem()
    {
        var raw = Tree([C("1", null, "A")], [D("100", "Màu", "Dropdown", 1, V("1", "Đỏ"), V("2", " đỏ\u200b"))]);

        Assert.Equal(["value.duplicate_name"], Codes(raw));
    }

    [Fact]
    public void Ac_4_3_attribute_without_values_is_free_text_whatever_its_input_type_or_marker()
    {
        var source = Build(Tree([C("1", null, "A")], [Open(D("100", "x", "Whatever", 9))], Entry("1", L("100"))));

        var attribute = source.Attributes.Single();
        Assert.Equal((AttributeKind.Text, false, 1, false), (attribute.Kind, attribute.AllowsMultiple, attribute.MaxValues, attribute.AllowsCustomValue));
        Assert.Empty(attribute.ValueIds);
    }

    [Fact]
    public void Dropdown_is_single_and_checkbox_is_multiple_with_its_maximum()
    {
        var source = Build(Baseline());

        var brand = source.Attributes.Single(a => a.Id == 100);
        Assert.Equal((AttributeKind.List, false, 1), (brand.Kind, brand.AllowsMultiple, brand.MaxValues));
        var color = source.Attributes.Single(a => a.Id == 101);
        Assert.Equal((AttributeKind.List, true, 5), (color.Kind, color.AllowsMultiple, color.MaxValues));
        Assert.Equal([1100, 1101], color.ValueIds);
        Assert.Equal([0, 1], source.Values.Where(v => v.AttributeId == 101).Select(v => v.Position));
    }

    [Fact]
    public void An_open_list_keeps_its_marker_and_its_values_in_file_order()
    {
        var brand = Build(Tree([C("1", null, "A")], [Open(D("100", "Thương hiệu", "Dropdown", 1, V("1001", "Samsung"), V("1000", "Apple")))])).Attributes.Single();

        Assert.True(brand.AllowsCustomValue);
        Assert.Equal([1001, 1000], brand.ValueIds);
    }

    [Fact]
    public void An_image_is_kept_on_a_top_level_category_and_dropped_below_it()
    {
        var source = Build(Tree([C("1", null, "A", "https://example.com/a.jpg"), C("10", "1", "B", "not even a url")]));

        Assert.Equal("https://example.com/a.jpg", source.Categories.Single(c => c.Id == 1).ImageUrl);
        Assert.Null(source.Categories.Single(c => c.Id == 10).ImageUrl);
    }

    [Theory]
    [InlineData("http://example.com/a.jpg")]
    [InlineData("/relative/a.jpg")]
    [InlineData("not a url")]
    public void A_top_level_image_that_is_not_an_absolute_https_url_is_invalid(string url) =>
        Assert.Equal(["category.image_invalid"], Codes(Tree([C("1", null, "A", url)])));

    [Fact]
    public void A_top_level_image_longer_than_500_characters_is_invalid() =>
        Assert.Equal(["category.image_invalid"], Codes(Tree([C("1", null, "A", "https://example.com/" + new string('a', 490))])));
}
