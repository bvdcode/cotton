// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Text.Json;

namespace Cotton.Previews
{
    internal static class FlatMetadataWriter
    {
        internal const int MaxTextLength = 1024;

        public static string EscapeKey(string value)
        {
            return value.Replace("~", "~0", StringComparison.Ordinal)
                .Replace(".", "~1", StringComparison.Ordinal)
                .Replace("\0", "~2", StringComparison.Ordinal);
        }
        public static void WriteText(Dictionary<string, string> target, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxTextLength)
            {
                return;
            }
            if (value.Contains('\0'))
            {
                target[key] = JsonSerializer.Serialize(value);
                target[$"{key}.encoding"] = "json";
                return;
            }
            target[key] = value;
        }

    }
}
