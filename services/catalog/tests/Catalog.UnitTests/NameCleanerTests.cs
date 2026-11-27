using System.Text;
using Catalog.Domain.Categories;
using Xunit;

namespace Catalog.UnitTests;

public sealed class NameCleanerTests
{
    [Theory]
    [InlineData("  Điện thoại  ", "Điện thoại")]
    [InlineData("Đỏ​", "Đỏ")]
    [InlineData("﻿Samsung", "Samsung")]
    [InlineData("A\u0000B\u001FC", "ABC")]
    [InlineData("Nồi \t chảo\n", "Nồi chảo")]
    public void Removes_invisible_characters_and_collapses_spaces(string raw, string expected) =>
        Assert.Equal(expected, NameCleaner.Clean(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("​\u0000")]
    public void Nothing_left_is_missing(string? raw) => Assert.Null(NameCleaner.Clean(raw));

    [Fact]
    public void Decomposed_vietnamese_is_stored_composed()
    {
        var decomposed = "Đỏ".Normalize(NormalizationForm.FormD);
        var cleaned = NameCleaner.Clean(decomposed);

        Assert.Equal("Đỏ", cleaned);
        Assert.True(cleaned!.IsNormalized(NormalizationForm.FormC));
    }
}
