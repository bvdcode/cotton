// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Xml.Linq;

namespace Cotton.TextExtraction
{
    public class OpenDocumentTextExtractor(ILogger<OpenDocumentTextExtractor> logger) : IFileTextExtractor
    {
        public static readonly string[] ContentTypes =
        [
            "application/vnd.oasis.opendocument.text",
            "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.presentation",
        ];

        public IEnumerable<string> SupportedContentTypes => ContentTypes;

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
                using ZipArchive archive = new(seekable.Stream, ZipArchiveMode.Read, leaveOpen: true);
                ZipArchiveEntry contentEntry = archive.GetEntry("content.xml")
                    ?? throw new InvalidDataException("The OpenDocument package has no content.xml entry.");
                await using Stream content = await contentEntry.OpenAsync(cancellationToken);
                XDocument document = await XDocument.LoadAsync(content, LoadOptions.None, cancellationToken);
                return TextExtractionUtilities.JoinLines(document.Descendants()
                    .Where(element => element.Name.LocalName is "h" or "p")
                    .Select(element => element.Value));
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                logger.LogWarning(ex, "Unable to extract OpenDocument text.");
                throw new FileTextExtractionException("Unable to read OpenDocument text.", ex);
            }
        }
    }
}
