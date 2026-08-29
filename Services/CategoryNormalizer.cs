using System.Globalization;
using System.Text.RegularExpressions;

namespace TheCertMaster.Services
{
    public static class CategoryNormalizer
    {
        public static string Normalize(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "Uncategorized";

            var collapsed = Regex.Replace(category.Trim(), @"\s+", " ");
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
        }
    }
}
