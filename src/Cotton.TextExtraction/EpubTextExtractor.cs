// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Xml.Linq;

namespace Cotton.TextExtraction
{
    public class EpubTextExtractor(ILogger<EpubTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/epub+zip";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            try
            {
                await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
                using ZipArchive archive = new(seekable.Stream, ZipArchiveMode.Read, leaveOpen: true);
                string packagePath = await GetPackagePathAsync(archive, cancellationToken);
                ZipArchiveEntry packageEntry = GetEntry(archive, packagePath);
                XDocument package = await LoadXmlAsync(packageEntry, cancellationToken);
                Dictionary<string, string> manifest = package.Descendants()
                    .Where(element => element.Name.LocalName == "item")
                    .Where(element => (string?)element.Attribute("media-type") is "application/xhtml+xml" or "text/html")
                    .ToDictionary(
                        element => (string?)element.Attribute("id")
                            ?? throw new InvalidDataException("An EPUB manifest item has no id."),
                        element => (string?)element.Attribute("href")
                            ?? throw new InvalidDataException("An EPUB manifest item has no href."),
                        StringComparer.Ordinal);
                string packageDirectory = GetDirectory(packagePath);
                text.AppendLines(package.Descendants()
                    .Where(element => element.Name.LocalName is "title" or "creator" or "subject" or "description")
                    .Select(element => element.Value), cancellationToken);
                foreach (XElement itemReference in package.Descendants()
                    .Where(element => element.Name.LocalName == "itemref"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (text.IsTruncated)
                    {
                        return;
                    }
                    string id = (string?)itemReference.Attribute("idref")
                        ?? throw new InvalidDataException("An EPUB spine item has no idref.");
                    if (!manifest.TryGetValue(id, out string? relativePath))
                    {
                        continue;
                    }
                    ZipArchiveEntry contentEntry = GetEntry(archive, ResolvePath(packageDirectory, relativePath));
                    await using Stream content = await contentEntry.OpenAsync(cancellationToken);
                    await HtmlTextExtractor.ExtractIntoAsync(content, text, cancellationToken);
                    text.AppendLineBreak();
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                logger.LogWarning(ex, "Unable to extract EPUB text.");
                throw new FileTextExtractionException("Unable to read EPUB text.", ex);
            }
        }

        private static async Task<string> GetPackagePathAsync(
            ZipArchive archive,
            CancellationToken cancellationToken)
        {
            XDocument container = await LoadXmlAsync(GetEntry(archive, "META-INF/container.xml"), cancellationToken);
            return (string?)container.Descendants()
                .SingleOrDefault(element => element.Name.LocalName == "rootfile")?
                .Attribute("full-path")
                ?? throw new InvalidDataException("The EPUB container has no package path.");
        }

        private static async Task<XDocument> LoadXmlAsync(
            ZipArchiveEntry entry,
            CancellationToken cancellationToken)
        {
            await using Stream stream = await entry.OpenAsync(cancellationToken);
            return await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        }

        private static ZipArchiveEntry GetEntry(ZipArchive archive, string path)
        {
            return archive.GetEntry(path)
                ?? throw new InvalidDataException($"The EPUB entry '{path}' is missing.");
        }

        private static string GetDirectory(string path)
        {
            int separator = path.LastIndexOf('/');
            return separator < 0 ? string.Empty : path[..(separator + 1)];
        }

        private static string ResolvePath(string directory, string relativePath)
        {
            string pathWithoutFragment = relativePath.Split('#', 2)[0];
            Uri baseUri = new($"https://epub.local/{directory}");
            Uri resolved = new(baseUri, pathWithoutFragment);
            return Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'));
        }
    }
}
