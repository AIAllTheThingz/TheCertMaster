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
    [InlineData("  nETWORK\tsecurity  ", "Network Security")]
    [InlineData("\u00a0 Network\u2003security\n", "Network Security")]
    [InlineData("security / identity", "Security / Identity")]
    public void Normalize_handles_blank_whitespace_casing_and_separators(string? input, string expected)
    {
        Assert.Equal(expected, CategoryNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_uses_invariant_casing()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("Internet Identity", CategoryNormalizer.Normalize("internet identity"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
