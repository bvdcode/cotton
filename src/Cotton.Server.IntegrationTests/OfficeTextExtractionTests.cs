// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace Cotton.Server.IntegrationTests
{
    public class OfficeTextExtractionTests
    {
        [Test]
        public async Task Extractors_LimitTextAcrossOfficeFormats()
        {
            using MemoryStream word = CreateWordDocument("First paragraph", "Second paragraph");
            using MemoryStream presentation = CreatePresentation("First slide", "Second slide");
            using MemoryStream spreadsheet = CreateSpreadsheet();

            Assert.That(await new WordDocumentTextExtractor(NullLogger<WordDocumentTextExtractor>.Instance)
                .ExtractAsync(word, 5), Is.EqualTo(new TextExtractionResult("First", true)));
            Assert.That(await new PresentationTextExtractor(NullLogger<PresentationTextExtractor>.Instance)
                .ExtractAsync(presentation, 5), Is.EqualTo(new TextExtractionResult("First", true)));
            Assert.That(await new SpreadsheetTextExtractor(NullLogger<SpreadsheetTextExtractor>.Instance)
                .ExtractAsync(spreadsheet, 5), Is.EqualTo(new TextExtractionResult("Peopl", true)));
        }

        [Test]
        public async Task WordExtractor_ReadsParagraphs()
        {
            using MemoryStream source = CreateWordDocument("First paragraph", "Second paragraph");
            WordDocumentTextExtractor extractor = new(NullLogger<WordDocumentTextExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo(string.Join(Environment.NewLine, "First paragraph", "Second paragraph")));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task PresentationExtractor_ReadsSlidesInOrder()
        {
            using MemoryStream source = CreatePresentation("First slide", "Second slide");
            PresentationTextExtractor extractor = new(NullLogger<PresentationTextExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo(string.Join(Environment.NewLine, "First slide", "Second slide")));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task SpreadsheetExtractor_ReadsSheetNamesAndRows()
        {
            using MemoryStream source = CreateSpreadsheet();
            SpreadsheetTextExtractor extractor = new(NullLogger<SpreadsheetTextExtractor>.Instance);

            string text = (await extractor.ExtractAsync(source)).Text;

            Assert.Multiple(() =>
            {
                Assert.That(text, Is.EqualTo(string.Join(Environment.NewLine, "People", "Name Age", "Alice 42")));
                Assert.That(source.CanRead, Is.True);
            });
        }

        [Test]
        public async Task SpreadsheetExtractor_ReadsSharedAndInlineStrings()
        {
            using MemoryStream source = new();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                source, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook, autoSave: true))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                SharedStringTablePart stringsPart = workbookPart.AddNewPart<SharedStringTablePart>();
                stringsPart.SharedStringTable = new SharedStringTable(
                    new SharedStringItem(new DocumentFormat.OpenXml.Spreadsheet.Text("Shared")));
                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData(new Row(
                    new Cell { DataType = CellValues.SharedString, CellValue = new CellValue("0") },
                    new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(
                        new DocumentFormat.OpenXml.Spreadsheet.Text("Inline")) })));
                workbookPart.Workbook = new Workbook(new Sheets(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet",
                }));
            }
            source.Position = 0;

            TextExtractionResult result = await new SpreadsheetTextExtractor(
                NullLogger<SpreadsheetTextExtractor>.Instance).ExtractAsync(source);

            Assert.That(result, Is.EqualTo(new TextExtractionResult(
                string.Join(Environment.NewLine, "Sheet", "Shared Inline"), false)));
        }

        [Test]
        public async Task SpreadsheetExtractor_TruncatesBeforeReadingLargeWorksheet()
        {
            using MemoryStream source = new();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                source, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook, autoSave: true))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                using (OpenXmlWriter writer = OpenXmlWriter.Create(worksheetPart))
                {
                    writer.WriteStartElement(new Worksheet());
                    writer.WriteStartElement(new SheetData());
                    for (int index = 0; index < 2_000; index++)
                    {
                        writer.WriteElement(CreateRow("Value", index.ToString()));
                    }
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                workbookPart.Workbook = new Workbook(new Sheets(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet",
                }));
            }
            source.Position = 0;

            TextExtractionResult result = await new SpreadsheetTextExtractor(
                NullLogger<SpreadsheetTextExtractor>.Instance).ExtractAsync(source, 6);

            Assert.That(result, Is.EqualTo(new TextExtractionResult("Sheet", true)));
        }

        [Test]
        public void SpreadsheetExtractor_RejectsOversizedSharedStringTable()
        {
            using MemoryStream source = new();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                source, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook, autoSave: true))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                SharedStringTablePart stringsPart = workbookPart.AddNewPart<SharedStringTablePart>();
                using (OpenXmlWriter writer = OpenXmlWriter.Create(stringsPart))
                {
                    writer.WriteStartElement(new SharedStringTable());
                    string value = new('x', 1024);
                    for (int index = 0; index < 8_193; index++)
                    {
                        writer.WriteElement(new SharedStringItem(
                            new DocumentFormat.OpenXml.Spreadsheet.Text(value)));
                    }
                    writer.WriteEndElement();
                }
                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());
                workbookPart.Workbook = new Workbook(new Sheets(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Sheet",
                }));
            }
            source.Position = 0;

            FileTextExtractionException? error = Assert.ThrowsAsync<FileTextExtractionException>(async () =>
                await new SpreadsheetTextExtractor(NullLogger<SpreadsheetTextExtractor>.Instance)
                    .ExtractAsync(source));

            Assert.That(error?.InnerException, Is.TypeOf<InvalidDataException>());
        }

        [Test]
        public void Extractors_InvalidPackagesReportUnreadableContent()
        {
            byte[] invalidPackage = [1, 2, 3, 4];
            using MemoryStream word = new(invalidPackage);
            using MemoryStream presentation = new(invalidPackage);
            using MemoryStream spreadsheet = new(invalidPackage);

            Assert.Multiple(() =>
            {
                Assert.ThrowsAsync<FileTextExtractionException>(async () =>
                    await new WordDocumentTextExtractor(NullLogger<WordDocumentTextExtractor>.Instance)
                        .ExtractAsync(word));
                Assert.ThrowsAsync<FileTextExtractionException>(async () =>
                    await new PresentationTextExtractor(NullLogger<PresentationTextExtractor>.Instance)
                        .ExtractAsync(presentation));
                Assert.ThrowsAsync<FileTextExtractionException>(async () =>
                    await new SpreadsheetTextExtractor(NullLogger<SpreadsheetTextExtractor>.Instance)
                        .ExtractAsync(spreadsheet));
            });
        }

        private static MemoryStream CreateWordDocument(params string[] paragraphs)
        {
            MemoryStream stream = new();
            using (WordprocessingDocument document = WordprocessingDocument.Create(
                stream,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                autoSave: true))
            {
                MainDocumentPart mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document(new Body(paragraphs.Select(text =>
                    new Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.Text(text))))));
            }
            stream.Position = 0;
            return stream;
        }

        private static MemoryStream CreatePresentation(params string[] slides)
        {
            MemoryStream stream = new();
            using (PresentationDocument document = PresentationDocument.Create(
                stream,
                DocumentFormat.OpenXml.PresentationDocumentType.Presentation,
                autoSave: true))
            {
                PresentationPart presentationPart = document.AddPresentationPart();
                SlideIdList slideIds = new();
                presentationPart.Presentation = new Presentation(slideIds);
                uint id = 256;
                foreach (string text in slides)
                {
                    SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
                    slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree(
                        new Shape(new TextBody(
                            new Drawing.BodyProperties(),
                            new Drawing.ListStyle(),
                            new Drawing.Paragraph(new Drawing.Run(new Drawing.Text(text))))))));
                    slideIds.Append(new SlideId
                    {
                        Id = id++,
                        RelationshipId = presentationPart.GetIdOfPart(slidePart),
                    });
                }
            }
            stream.Position = 0;
            return stream;
        }

        private static MemoryStream CreateSpreadsheet()
        {
            MemoryStream stream = new();
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                stream,
                DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook,
                autoSave: true))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData(
                    CreateRow("Name", "Age"),
                    CreateRow("Alice", "42")));
                workbookPart.Workbook = new Workbook(new Sheets(new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "People",
                }));
            }
            stream.Position = 0;
            return stream;
        }

        private static Row CreateRow(params string[] values)
        {
            return new Row(values.Select(value => new Cell
            {
                DataType = CellValues.String,
                CellValue = new CellValue(value),
            }));
        }
    }
}
