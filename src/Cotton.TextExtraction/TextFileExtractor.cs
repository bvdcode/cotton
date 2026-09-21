// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Text;

namespace Cotton.TextExtraction
{
    public class TextFileExtractor(ILogger<TextFileExtractor> logger) : IFileTextExtractor
    {
        public static readonly string[] ContentTypes =
        [
            "text/plain",
            "text/markdown",
            "text/x-markdown",
            "text/csv",
            "text/tab-separated-values",
            "text/css",
            "text/javascript",
            "text/typescript",
            "text/xml",
            "text/yaml",
            "application/json",
            "application/javascript",
            "application/typescript",
            "application/xml",
            "application/yaml",
            "application/x-yaml",
        ];

        public IEnumerable<string> SupportedContentTypes => ContentTypes;

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using StreamReader reader = new(
                    source,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                return (await reader.ReadToEndAsync(cancellationToken)).Trim();
            }
            catch (DecoderFallbackException ex)
            {
                logger.LogWarning(ex, "Unable to decode text file.");
                throw new FileTextExtractionException("Unable to decode text file.", ex);
            }
        }
    }
}
