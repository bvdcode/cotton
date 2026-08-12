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
                    ParseFormatTags(root, limits));
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
