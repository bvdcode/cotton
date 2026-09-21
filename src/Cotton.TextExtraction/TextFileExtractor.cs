// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.Text;

namespace Cotton.TextExtraction
{
    public class TextFileExtractor(ILogger<TextFileExtractor> logger) : FileTextExtractor
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
            "text/calendar",
            "text/vcard",
            "text/x-vcard",
            "application/json",
            "application/javascript",
            "application/typescript",
            "application/xml",
            "application/yaml",
            "application/x-yaml",
        ];

        public override IEnumerable<string> SupportedContentTypes => ContentTypes;

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            try
            {
                using StreamReader reader = new(
                    source,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                char[] buffer = new char[4096];
                while (!text.IsTruncated)
                {
                    int count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                    if (count == 0)
                    {
                        break;
                    }
                    text.Append(buffer.AsSpan(0, count));
                }
            }
            catch (DecoderFallbackException ex)
            {
                logger.LogWarning(ex, "Unable to decode text file.");
                throw new FileTextExtractionException("Unable to decode text file.", ex);
            }
        }
    }
}
