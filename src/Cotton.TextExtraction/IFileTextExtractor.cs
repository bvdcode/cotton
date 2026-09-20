// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    public interface IFileTextExtractor
    {
        IEnumerable<string> SupportedContentTypes { get; }

        Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default);
    }
}
