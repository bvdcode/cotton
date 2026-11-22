// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents.Pdf;
using Microsoft.Extensions.Logging;

namespace Cotton.TextExtraction
{
    public class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : DocumentTextExtractor(logger)
    {
        public const string ContentType = "application/pdf";
        public const string FileExtension = ".pdf";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ReadTextAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            using PdfDocument document = await PdfReader.ReadAsync(source, cancellationToken: cancellationToken);
            text.AppendLines(document.ReadText(cancellationToken: cancellationToken).Select(block => block.Text), cancellationToken);
        }
    }
}
