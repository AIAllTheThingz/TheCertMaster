using System.Text.RegularExpressions;

namespace TheCertMaster.Services
{
    public static class CategoryNormalizer
    {
        /// <summary>
        /// Collapses whitespace but preserves the originally-imported display casing
        /// (e.g. "CompTIA Security+" stays "CompTIA Security+", not "Comptia Security+").
        /// Use <see cref="NormalizeKey"/> for case-insensitive matching/de-duplication.
        /// </summary>
        public static string Normalize(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return "Uncategorized";

            return Regex.Replace(category.Trim(), @"\s+", " ");
        }

        /// <summary>
        /// Case-insensitive key for matching/grouping categories that only differ by casing.
        /// Not for display.
        /// </summary>
        public static string NormalizeKey(string? category)
        {
            return Normalize(category).ToLowerInvariant();
        }
    }
}
