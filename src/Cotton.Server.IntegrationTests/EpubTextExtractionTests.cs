// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.IO.Compression;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class EpubTextExtractionTests
    {
        [Test]
        public async Task Extract_ReadsSpineInOrder()
        {
            using MemoryStream source = CreateEpub();
            EpubTextExtractor extractor = new(NullLogger<EpubTextExtractor>.Instance);

            string text = await extractor.ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo(string.Join(Environment.NewLine,
                    "First chapter", "First body", "Second chapter", "Second body")));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public void Extract_InvalidEpubReportsUnreadableContent()
        {
            using MemoryStream source = new([1, 2, 3, 4]);
            EpubTextExtractor extractor = new(NullLogger<EpubTextExtractor>.Instance);

            Assert.ThrowsAsync<FileTextExtractionException>(async () => await extractor.ExtractAsync(source));
        }

        private static MemoryStream CreateEpub()
        {
            MemoryStream stream = new();
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "META-INF/container.xml",
                    "<container xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">"
                    + "<rootfiles><rootfile full-path=\"OEBPS/content.opf\" /></rootfiles></container>");
                AddEntry(archive, "OEBPS/content.opf",
                    "<package xmlns=\"http://www.idpf.org/2007/opf\"><manifest>"
                    + "<item id=\"first\" href=\"first.xhtml\" media-type=\"application/xhtml+xml\" />"
                    + "<item id=\"second\" href=\"second.xhtml\" media-type=\"application/xhtml+xml\" />"
                    + "</manifest><spine><itemref idref=\"first\"/><itemref idref=\"second\"/></spine></package>");
                AddEntry(archive, "OEBPS/first.xhtml", "<html><body><h1>First chapter</h1><p>First body</p></body></html>");
                AddEntry(archive, "OEBPS/second.xhtml", "<html><body><h1>Second chapter</h1><p>Second body</p></body></html>");
            }
            stream.Position = 0;
            return stream;
        }

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
