// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.TextExtraction
{
    public abstract class FileTextExtractor : IFileTextExtractor
    {
        public const int DefaultMaxUtf8Bytes = 256 * 1024 * 1024;

        public abstract IEnumerable<string> SupportedContentTypes { get; }

        public async Task<TextExtractionResult> ExtractAsync(
            Stream source, int maxUtf8Bytes = DefaultMaxUtf8Bytes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            TextExtractionBuffer text = new(maxUtf8Bytes);
            await ExtractAsync(source, text, cancellationToken);
            return text.GetResult();
        }

        protected abstract Task ExtractAsync(
            Stream source, TextExtractionBuffer text, CancellationToken cancellationToken);
    }
}
