// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;

namespace Cotton.TextExtraction
{
    public class WordDocumentTextExtractor(ILogger<WordDocumentTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
                using WordprocessingDocument document = WordprocessingDocument.Open(seekable.Stream, false);
                MainDocumentPart mainPart = document.MainDocumentPart
                    ?? throw new FileFormatException("The document has no main part.");
                Document wordDocument = mainPart.Document
                    ?? throw new FileFormatException("The document body is missing.");
                IEnumerable<Paragraph> paragraphs = wordDocument.Body?.Descendants<Paragraph>() ?? [];
                IEnumerable<Paragraph> headers = mainPart.HeaderParts.SelectMany(part =>
                    part.Header?.Descendants<Paragraph>() ?? []);
                IEnumerable<Paragraph> footers = mainPart.FooterParts.SelectMany(part =>
                    part.Footer?.Descendants<Paragraph>() ?? []);
                return TextExtractionUtilities.JoinLines(paragraphs.Concat(headers).Concat(footers)
                    .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text))));
            }
            catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to extract Word document text.");
                throw new FileTextExtractionException("Unable to read Word document text.", ex);
            }
        }
    }
}
