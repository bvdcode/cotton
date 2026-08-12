// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    public class FileContentMetadataExtractorProvider(IEnumerable<IFileContentMetadataExtractor> extractors)
    {
        private readonly IReadOnlyList<IFileContentMetadataExtractor> _extractors = [.. extractors];

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
