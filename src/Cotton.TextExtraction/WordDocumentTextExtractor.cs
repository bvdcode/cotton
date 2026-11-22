// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents;
using Mediana.Documents.Word;
using Microsoft.Extensions.Logging;

namespace Cotton.TextExtraction
{
    public class WordDocumentTextExtractor(ILogger<WordDocumentTextExtractor> logger) : DocumentTextExtractor(logger)
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ReadTextAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            byte[] input = await ReadDocumentAsync(source, cancellationToken);
            IEnumerable<DocumentTextBlock> blocks = WordTextReader.Read(input,
                new() { IncludeHeaders = true, IncludeFooters = true }, cancellationToken: cancellationToken);
            text.AppendLines(blocks.Select(block => block.Text), cancellationToken);
        }
    }
}
