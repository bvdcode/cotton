// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text.Json;

namespace Cotton.Previews
{
    internal static class FlatMetadataWriter
    {
        public static string EscapeKey(string value)
        {
            return value.Replace("~", "~0", StringComparison.Ordinal)
                .Replace(".", "~1", StringComparison.Ordinal)
                .Replace("\0", "~2", StringComparison.Ordinal);
        }

        public static void WriteText(Dictionary<string, string> target, string key, string value)
        {
            if (value.Contains('\0'))
            {
                target[key] = JsonSerializer.Serialize(value);
                target[$"{key}.encoding"] = "json";
                return;
            }
            target[key] = value;
        }

        public static void WriteJson(Dictionary<string, string> target, string key, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in value.EnumerateObject())
                    {
                        WriteJson(target, $"{key}.{EscapeKey(property.Name)}", property.Value);
                    }
                    if (!value.EnumerateObject().Any())
                    {
                        target[key] = "{}";
                    }
                    break;
                case JsonValueKind.Array:
                    int index = 0;
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        WriteJson(target, $"{key}.{index.ToString(CultureInfo.InvariantCulture)}", item);
                        index++;
                    }
                    if (index == 0)
                    {
                        target[key] = "[]";
                    }
                    break;
                case JsonValueKind.String:
                    WriteText(target, key, value.GetString()!);
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    target[key] = value.GetRawText();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value.ValueKind, "Unsupported JSON value.");
            }
        }
    }
}
