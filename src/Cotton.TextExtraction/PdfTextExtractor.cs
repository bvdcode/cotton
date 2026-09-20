// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Docnet.Core;
using Docnet.Core.Exceptions;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Cotton.TextExtraction
{
    public class PdfTextExtractor(ILogger<PdfTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "application/pdf";
        public const string FileExtension = ".pdf";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(Path.GetTempPath(), $"cotton-text-{Guid.NewGuid():N}.pdf");
            try
            {
                await using (FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await source.CopyToAsync(file, cancellationToken);
                }

                using IDocReader document = DocLib.Instance.GetDocReader(path, new PageDimensions(1));
                StringBuilder text = new();
                int pageCount = document.GetPageCount();
                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using IPageReader page = document.GetPageReader(pageIndex);
                    text.AppendLine(page.GetText());
                }
                return text.ToString().Trim();
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
