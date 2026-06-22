// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Validators;
using System.Text;
using System.Text.RegularExpressions;

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Builds normalized search criteria from user input.
    /// </summary>
    public static class LayoutSearchCriteriaBuilder
    {
        private const string LikeEscape = "\\";

        private static readonly Regex GuidRegex = new(
            @"(?<![0-9a-fA-F])(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32})(?![0-9a-fA-F])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TextTokenRegex = new(
            @"[\p{L}\p{N}]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Builds criteria for all registered layout search providers.
        /// </summary>
        public static LayoutSearchCriteria Build(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new BadHttpRequestException("Query cannot be empty.");
            }

            string rawQuery = query.Normalize(NormalizationForm.FormC).Trim();

            Guid[] ids = ExtractGuids(rawQuery)
                .Distinct()
                .ToArray();

            string textWithoutGuids = GuidRegex.Replace(rawQuery, " ").Trim();
            string nameKey = NameValidator.GetNameKey(textWithoutGuids);
            string escapedNameKey = EscapeLike(nameKey);

            return new LayoutSearchCriteria(
                NameKey: nameKey,
                ContainsPattern: nameKey.Length == 0 ? string.Empty : $"%{escapedNameKey}%",
                PrefixPattern: nameKey.Length == 0 ? string.Empty : $"{escapedNameKey}%",
                LikeEscape: LikeEscape,
                TextTokens: BuildTextTokens(nameKey),
                IdQueries: ids);
        }

        private static LayoutSearchToken[] BuildTextTokens(string nameKey)
        {
            if (nameKey.Length == 0)
            {
                return [];
            }

            return TextTokenRegex
                .Matches(nameKey)
                .Select(x => x.Value)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(x =>
                {
                    string escapedToken = EscapeLike(x);
                    return new LayoutSearchToken(
                        NameKey: x,
                        ContainsPattern: $"%{escapedToken}%",
                        HasLetters: x.Any(char.IsLetter));
                })
                .ToArray();
        }

        private static Guid[] ExtractGuids(string value)
        {
            List<Guid> result = [];

            foreach (Match match in GuidRegex.Matches(value))
            {
                if (Guid.TryParse(match.Value, out Guid guid) && !result.Contains(guid))
                {
                    result.Add(guid);
                }
            }

            if (result.Count == 0 && Guid.TryParse(value, out Guid parsed))
            {
                result.Add(parsed);
            }

            return result.ToArray();
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
        }
    }
}
