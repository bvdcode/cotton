// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    public interface IFileTextExtractor
    {
        IEnumerable<string> SupportedContentTypes { get; }

        Task<TextExtractionResult> ExtractAsync(Stream source,
            int maxUtf8Bytes = FileTextExtractor.DefaultMaxUtf8Bytes, CancellationToken cancellationToken = default);
    }
}
