// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Cotton.Previews
{
    internal static class FfprobeJsonParser
    {
        public static MediaProbeInfo? ParseMediaProbe(string raw)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(raw);
                JsonElement root = document.RootElement;

                return new MediaProbeInfo(
                    ParseProbeDuration(root),
                    ParseFirstStreamCodec(root, "video"),
                    ParseFirstStreamCodec(root, "audio"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static MediaMetadataInfo? ParseMediaMetadata(
            string raw,
            MediaMetadataProbeLimits limits)
        {
            ArgumentNullException.ThrowIfNull(limits);

            try
            {
                using JsonDocument document = JsonDocument.Parse(raw);
                JsonElement root = document.RootElement;

                return new MediaMetadataInfo(
                    ParseProbeDuration(root),
                    ParseFirstStreamCodec(root, "video"),
                    ParseFirstStreamCodec(root, "audio"),
                    ParseFirstVideoStreamInt(root, "width"),
                    ParseFirstVideoStreamInt(root, "height"),
                    ParseFormatTags(root, limits),
                    FlattenMetadata(root));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static double? ParseProbeDuration(JsonElement root)
        {
            return root.TryGetProperty("format", out JsonElement format)
                && format.TryGetProperty("duration", out JsonElement durationElement)
                    ? ParsePositiveDuration(durationElement.GetString() ?? string.Empty)
                    : null;
        }

        private static IReadOnlyDictionary<string, string> FlattenMetadata(JsonElement root)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            if (root.TryGetProperty("format", out JsonElement format))
            {
                AddProperties(result, "media.format", format, MediaMetadataFields.FormatProperties);
                AddTags(result, "media.format.tags", format,
                    ["location", "location-eng", "com.apple.quicktime.location.ISO6709"]);
            }
            if (root.TryGetProperty("streams", out JsonElement streams))
            {
                int index = 0;
                foreach (JsonElement stream in streams.EnumerateArray())
                {
                    string key = $"media.streams.{index.ToString(CultureInfo.InvariantCulture)}";
                    AddProperties(result, key, stream, MediaMetadataFields.StreamProperties);
                    AddTags(result, $"{key}.tags", stream, MediaMetadataFields.StreamTags);
                    if (stream.TryGetProperty("side_data_list", out JsonElement sideData))
                    {
                        int sideIndex = 0;
                        foreach (JsonElement side in sideData.EnumerateArray())
                        {
                            AddProperties(result, $"{key}.side_data_list.{sideIndex.ToString(CultureInfo.InvariantCulture)}", side, ["rotation"]);
                            sideIndex++;
                        }
                    }
                    index++;
                }
            }
            return result;
        }

        private static void AddTags(Dictionary<string, string> target, string key, JsonElement parent, string[] names)
        {
            if (!parent.TryGetProperty("tags", out JsonElement tags) || tags.ValueKind != JsonValueKind.Object)
            {
                return;
            }
            foreach (JsonProperty tag in tags.EnumerateObject())
            {
                if (names.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
                {
                    AddScalar(target, $"{key}.{FlatMetadataWriter.EscapeKey(tag.Name.ToLowerInvariant())}", tag.Value);
                }
            }
        }

        private static void AddProperties(Dictionary<string, string> target, string key, JsonElement parent, string[] names)
        {
            foreach (string name in names)
            {
                if (parent.TryGetProperty(name, out JsonElement value))
                {
                    AddScalar(target, $"{key}.{name}", value);
                }
            }
        }

        private static void AddScalar(Dictionary<string, string> target, string key, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    FlatMetadataWriter.WriteText(target, key, value.GetString());
                    break;
                case JsonValueKind.Number:
                    FlatMetadataWriter.WriteText(target, key, value.GetRawText());
                    break;
            }
        }

        private static double? ParsePositiveDuration(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && value > 0
                    ? value
                    : null;
        }

        private static string? ParseFirstStreamCodec(JsonElement root, string targetCodecType)
        {
            if (!root.TryGetProperty("streams", out JsonElement streams))
            {
                return null;
            }

            foreach (JsonElement stream in streams.EnumerateArray())
            {
                if (TryReadStreamCodec(stream, out string? codecType, out string? codecName)
                    && codecType == targetCodecType)
                {
                    return codecName;
                }
            }

            return null;
        }

        private static int? ParseFirstVideoStreamInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty("streams", out JsonElement streams))
            {
                return null;
            }

            foreach (JsonElement stream in streams.EnumerateArray())
            {
                if (!TryReadStreamCodec(stream, out string? codecType, out _)
                    || codecType != "video"
                    || !stream.TryGetProperty(propertyName, out JsonElement valueElement))
                {
                    continue;
                }

                if (valueElement.ValueKind == JsonValueKind.Number
                    && valueElement.TryGetInt32(out int numericValue)
                    && numericValue > 0)
                {
                    return numericValue;
                }

                if (valueElement.ValueKind == JsonValueKind.String
                    && int.TryParse(
                        valueElement.GetString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int stringValue)
                    && stringValue > 0)
                {
                    return stringValue;
                }
            }

            return null;
        }

        private static IReadOnlyDictionary<string, string> ParseFormatTags(
            JsonElement root,
            MediaMetadataProbeLimits limits)
        {
            if (!root.TryGetProperty("format", out JsonElement format)
                || !format.TryGetProperty("tags", out JsonElement tags)
                || tags.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
            int totalBytes = 0;
            foreach (JsonProperty tag in tags.EnumerateObject())
            {
                if (!MediaMetadataFields.FormatTags.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
                string? value = tag.Value.ValueKind == JsonValueKind.String
                    ? tag.Value.GetString()
                    : tag.Value.ToString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string trimmed = value.Trim();
                int valueBytes = Encoding.UTF8.GetByteCount(trimmed);
                if (valueBytes > limits.MaxTagValueBytes)
                {
                    throw new MediaMetadataLimitExceededException(
                        "individual tag value",
                        limits.MaxTagValueBytes);
                }

                int keyBytes = Encoding.UTF8.GetByteCount(tag.Name);
                totalBytes = checked(totalBytes + keyBytes + valueBytes);
                if (totalBytes > limits.MaxTotalTagBytes)
                {
                    throw new MediaMetadataLimitExceededException(
                        "aggregate tag payload",
                        limits.MaxTotalTagBytes);
                }

                result[tag.Name] = trimmed;
            }

            return result;
        }

        private static bool TryReadStreamCodec(
            JsonElement stream,
            out string? codecType,
            out string? codecName)
        {
            codecType = null;
            codecName = null;

            if (!stream.TryGetProperty("codec_type", out JsonElement typeElement)
                || !stream.TryGetProperty("codec_name", out JsonElement codecElement))
            {
                return false;
            }

            codecType = typeElement.GetString();
            codecName = codecElement.GetString();
            return codecName is not null;
        }
    }
}
