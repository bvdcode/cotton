// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Merges content metadata while keeping non-managed manifest metadata intact.
    /// </summary>
    internal static class FileContentMetadataDictionary
    {
        public static Dictionary<string, string> ReplaceManagedValues(
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

            return result;
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
