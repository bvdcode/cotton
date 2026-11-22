// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.IO;
using Microsoft.Extensions.Logging;
using System.Xml;

namespace Cotton.TextExtraction
{
    public abstract class DocumentTextExtractor(ILogger logger) : FileTextExtractor
    {
        private const int MaximumSourceBytes = 64 * 1024 * 1024;

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            try
            {
                await ReadTextAsync(source, text, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or XmlException)
            {
                logger.LogWarning(ex, "Unable to extract document text with {Extractor}.", GetType().Name);
                throw new FileTextExtractionException("Unable to read document text.", ex);
            }
        }

        protected static Task<byte[]> ReadDocumentAsync(Stream source, CancellationToken cancellationToken)
            => BoundedStreamReader.ReadAsync(source, MaximumSourceBytes, cancellationToken);

        protected abstract Task ReadTextAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken);
    }
}
