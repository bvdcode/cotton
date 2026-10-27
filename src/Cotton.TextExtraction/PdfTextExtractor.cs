// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Cotton.TextExtraction
{
    public class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/pdf";
        public const string FileExtension = ".pdf";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            string path = Path.Combine(Path.GetTempPath(), $"cotton-text-{Guid.NewGuid():N}.pdf");
            try
            {
                await using (FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await source.CopyToAsync(file, cancellationToken);
                }

                using PdfDocument document = PdfDocument.Open(path);
                for (int pageNumber = 1; pageNumber <= document.NumberOfPages && !text.IsTruncated; pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Page page = document.GetPage(pageNumber);
                    text.AppendSeparated(ContentOrderTextExtractor.GetText(page));
                    text.AppendLineBreak();
                }
            }
            catch (Exception ex) when (ex is PdfDocumentFormatException or PdfDocumentEncryptedException)
            {
                logger.LogWarning(ex, "Unable to extract PDF text.");
                throw new FileTextExtractionException("Unable to read PDF text.", ex);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
