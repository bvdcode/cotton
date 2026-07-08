// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Resolves content metadata extractors by MIME type.
    /// </summary>
    public class FileContentMetadataExtractorProvider(IEnumerable<IFileContentMetadataExtractor> extractors)
    {
        /// <summary>
        /// Current extractor version for scheduling and idempotency.
        /// </summary>
        public const int CurrentVersion = 1;

        private readonly IReadOnlyList<IFileContentMetadataExtractor> _extractors = [.. extractors];

        /// <summary>
        /// Gets an extractor for the supplied content type.
        /// </summary>
        public IFileContentMetadataExtractor? GetExtractor(string contentType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

            foreach (IFileContentMetadataExtractor extractor in _extractors)
            {
                if (extractor.Supports(contentType))
                {
                    return extractor;
                }
            }

            return null;
        }
    }
}
