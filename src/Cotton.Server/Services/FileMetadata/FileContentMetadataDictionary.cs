// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Merges content metadata while keeping non-managed manifest metadata intact.
    /// </summary>
    internal static class FileContentMetadataDictionary
    {
        public static Dictionary<string, string>? ReplaceManagedValues(
            Dictionary<string, string>? current,
            IReadOnlyDictionary<string, string> extracted)
        {
            Dictionary<string, string> result = current is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(current, StringComparer.Ordinal);

            foreach (string key in result.Keys.Where(IsManagedKey).ToArray())
            {
                result.Remove(key);
            }

            foreach ((string key, string value) in extracted)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    result[key] = value;
                }
            }

            result[FileContentMetadataKeys.ExtractionProcessed] = "true";
            return result;
        }

        public static Dictionary<string, string> MarkProcessed(Dictionary<string, string>? current)
        {
            Dictionary<string, string> result = current is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(current, StringComparer.Ordinal);

            result[FileContentMetadataKeys.ExtractionProcessed] = "true";
            return result;
        }

        public static bool HasProcessedValues(Dictionary<string, string>? metadata)
        {
            return metadata is not null
                && (metadata.Count == 0
                    || metadata.ContainsKey(FileContentMetadataKeys.ExtractionProcessed)
                    || metadata.Keys.Any(IsManagedKey));
        }

        public static bool IsProjectionKey(string key)
        {
            return !string.Equals(
                key,
                FileContentMetadataKeys.ExtractionProcessed,
                StringComparison.Ordinal);
        }

        private static bool IsManagedKey(string key)
        {
            foreach (string prefix in FileContentMetadataKeys.ManagedPrefixes)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
