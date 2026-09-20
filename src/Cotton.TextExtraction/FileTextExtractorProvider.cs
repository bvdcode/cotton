// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    public class FileTextExtractorProvider(IEnumerable<IFileTextExtractor> extractors)
    {
        private readonly Dictionary<string, IFileTextExtractor> _extractors = extractors
            .SelectMany(extractor => extractor.SupportedContentTypes.Select(contentType => (contentType, extractor)))
            .ToDictionary(item => item.contentType, item => item.extractor, StringComparer.OrdinalIgnoreCase);

        public string[] GetSupportedContentTypes() => [.. _extractors.Keys];

        public IFileTextExtractor? GetExtractor(string contentType) => _extractors.GetValueOrDefault(contentType);
    }
}
