// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.TextExtraction
{
    internal static class TextExtractionUtilities
    {
        public static string JoinLines(IEnumerable<string?> lines)
        {
            return string.Join(Environment.NewLine, lines
                .SelectMany(line => line?.Split(["\r\n", "\r", "\n"], StringSplitOptions.None) ?? [])
                .Select(NormalizeWhitespace)
                .Where(line => line.Length > 0));
        }

        public static string NormalizeWhitespace(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder result = new(value.Length);
            bool pendingSpace = false;
            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    result.Append(' ');
                    pendingSpace = false;
                }
                result.Append(character);
            }
            return result.ToString();
        }
    }
}
