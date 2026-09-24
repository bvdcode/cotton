// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Docnet.Core;
using Docnet.Core.Exceptions;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Microsoft.Extensions.Logging;

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

                using IDocReader document = DocLib.Instance.GetDocReader(path, new PageDimensions(1));
                int pageCount = document.GetPageCount();
                for (int pageIndex = 0; pageIndex < pageCount && !text.IsTruncated; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using IPageReader page = document.GetPageReader(pageIndex);
                    text.AppendSeparated(page.GetText());
                    text.AppendLineBreak();
                }
            }
            catch (DocnetException ex)
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
