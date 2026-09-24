// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.IO.Compression;
using System.Text;

namespace Cotton.Server.IntegrationTests
{
    public class OpenDocumentTextExtractionTests
    {
        [Test]
        public async Task Extract_LimitIndexesBeginningOfFirstParagraph()
        {
            using MemoryStream source = CreateDocument();
            OpenDocumentTextExtractor extractor = new(NullLogger<OpenDocumentTextExtractor>.Instance);
            Assert.That(await extractor.ExtractAsync(source, 8), Is.EqualTo(new TextExtractionResult("Document", true)));
        }

        [Test]
        public async Task Extract_ReadsParagraphsAndKeepsSourceOpen()
        {
            using MemoryStream source = CreateDocument();
            OpenDocumentTextExtractor extractor = new(NullLogger<OpenDocumentTextExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo($"Document title{Environment.NewLine}First paragraph{Environment.NewLine}Cell value"));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public void Extract_InvalidPackageReportsUnreadableContent()
        {
            using MemoryStream source = new(Encoding.UTF8.GetBytes("not a package"));
            OpenDocumentTextExtractor extractor = new(NullLogger<OpenDocumentTextExtractor>.Instance);

            Assert.ThrowsAsync<FileTextExtractionException>(async () => await extractor.ExtractAsync(source));
        }

        [Test]
        public void Extract_CompressedOversizedXmlReportsUnreadableContent()
        {
            using MemoryStream source = new();
            using (ZipArchive archive = new(source, ZipArchiveMode.Create, leaveOpen: true))
            {
                using StreamWriter writer = new(archive.CreateEntry("content.xml").Open(), Encoding.UTF8);
                writer.Write("<root>");
                writer.Write(new string('x', 16 * 1024 * 1024));
                writer.Write("</root>");
            }
            Assert.That(source.Length, Is.LessThan(1024 * 1024));
            source.Position = 0;
            OpenDocumentTextExtractor extractor = new(NullLogger<OpenDocumentTextExtractor>.Instance);

            FileTextExtractionException? error = Assert.ThrowsAsync<FileTextExtractionException>(
                async () => await extractor.ExtractAsync(source));
            Assert.That(error?.InnerException?.Message, Does.Contain("size limit"));
        }

        private static MemoryStream CreateDocument()
        {
            MemoryStream stream = new();
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                ZipArchiveEntry entry = archive.CreateEntry("content.xml");
                using StreamWriter writer = new(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <office:document-content
                        xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                        xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                        xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0">
                      <office:body>
                        <office:text>
                          <text:h>Document title</text:h>
                          <text:p>First <text:span>paragraph</text:span></text:p>
                        </office:text>
                        <office:spreadsheet>
                          <table:table><table:table-row><table:table-cell><text:p>Cell value</text:p></table:table-cell></table:table-row></table:table>
                        </office:spreadsheet>
                      </office:body>
                    </office:document-content>
                    """);
            }
            stream.Position = 0;
            return stream;
        }
    }
}
