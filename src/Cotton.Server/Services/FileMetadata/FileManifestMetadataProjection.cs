// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Builds the metadata dictionary returned by file DTOs.
    /// </summary>
    internal static class FileManifestMetadataProjection
    {
        public static Dictionary<string, string> Merge(
            Dictionary<string, string>? nodeFileMetadata,
            Dictionary<string, string>? manifestMetadata)
        {
            Dictionary<string, string> result = nodeFileMetadata is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(nodeFileMetadata, StringComparer.Ordinal);

            if (manifestMetadata is null)
            {
                return result;
            }

            foreach ((string key, string value) in manifestMetadata)
            {
                if (FileContentMetadataDictionary.IsProjectionKey(key))
                {
                    result[key] = value;
                }
            }

            return result;
        }
    }
}
