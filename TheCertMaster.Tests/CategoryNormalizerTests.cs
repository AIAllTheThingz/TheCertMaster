using System.Globalization;
using TheCertMaster.Services;
using Xunit;

namespace TheCertMaster.Tests;

public class CategoryNormalizerTests
{
    [Theory]
    [InlineData(null, "Uncategorized")]
    [InlineData("", "Uncategorized")]
    [InlineData(" \t ", "Uncategorized")]
    [InlineData("  nETWORK\tsecurity  ", "nETWORK security")]
    [InlineData("  Network security\n", "Network security")]
    [InlineData("security / identity", "security / identity")]
    [InlineData("CompTIA Security+", "CompTIA Security+")]
    public void Normalize_collapses_whitespace_but_preserves_display_casing(string? input, string expected)
    {
        Assert.Equal(expected, CategoryNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null, "uncategorized")]
    [InlineData("", "uncategorized")]
    [InlineData("  AWS  ", "aws")]
    [InlineData("CompTIA Security+", "comptia security+")]
    public void NormalizeKey_returns_lowercase_key(string? input, string expected)
    {
        Assert.Equal(expected, CategoryNormalizer.NormalizeKey(input));
    }

    [Fact]
    public void NormalizeKey_treats_different_casings_of_the_same_category_as_equal()
    {
        Assert.Equal(CategoryNormalizer.NormalizeKey("AWS"), CategoryNormalizer.NormalizeKey("aws"));
        Assert.Equal(CategoryNormalizer.NormalizeKey("CompTIA Security+"), CategoryNormalizer.NormalizeKey("Comptia security+"));
    }

    [Fact]
    public void NormalizeKey_uses_invariant_casing()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("internet identity", CategoryNormalizer.NormalizeKey("Internet Identity"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
