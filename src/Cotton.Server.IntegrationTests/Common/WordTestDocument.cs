// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO.Compression;

namespace Cotton.Server.IntegrationTests.Common
{
    internal static class WordTestDocument
    {
        public static byte[] Create(bool missingHeader = false)
        {
            using MemoryStream stream = new();
            using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                MainDocumentPart main = document.AddMainDocumentPart();
                HeaderPart header = main.AddNewPart<HeaderPart>();
                header.Header = new Header(new Paragraph(new Run(new Text("Header text"))));
                FooterPart footer = main.AddNewPart<FooterPart>();
                footer.Footer = new Footer(new Paragraph(new Run(new Text("Footer text"))));
                main.Document = new Document(new Body(
                    new Paragraph(new Run(new Text("Body text"))),
                    new SectionProperties(
                        new HeaderReference { Id = main.GetIdOfPart(header), Type = HeaderFooterValues.Default },
                        new FooterReference { Id = main.GetIdOfPart(footer), Type = HeaderFooterValues.Default })));
            }
            if (missingHeader)
            {
                using ZipArchive archive = new(stream, ZipArchiveMode.Update, leaveOpen: true);
                archive.GetEntry("word/header1.xml")!.Delete();
            }
            return stream.ToArray();
        }
    }
}
