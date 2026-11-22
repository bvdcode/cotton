// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.IO.Compression;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace Cotton.Server.IntegrationTests
{
    public partial class OfficeTextExtractionTests
    {
        [Test]
        public async Task PresentationExtractor_ReadsDeclaredOrderAndSpeakerNotesWithoutSlideSize()
        {
            using MemoryStream source = CreatePresentation("First slide", "Second slide");
            using (PresentationDocument document = PresentationDocument.Open(source, true))
            {
                PresentationPart presentation = document.PresentationPart!;
                SlideIdList slideIds = presentation.Presentation!.SlideIdList!;
                List<SlideId> slides = slideIds.Elements<SlideId>().ToList();
                SlidePart slide = (SlidePart)presentation.GetPartById(slides[0].RelationshipId!);
                NotesSlidePart notes = slide.AddNewPart<NotesSlidePart>();
                Shape slideNumber = CreatePresentationTextShape("99");
                slideNumber.PrependChild(new NonVisualShapeProperties(
                    new NonVisualDrawingProperties { Id = 1, Name = "Slide number" },
                    new NonVisualShapeDrawingProperties(),
                    new ApplicationNonVisualDrawingProperties(new PlaceholderShape
                    {
                        Type = PlaceholderValues.SlideNumber,
                    })));
                notes.NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(
                    CreatePresentationTextShape("Заметки докладчика"), slideNumber)));
                slideIds.RemoveAllChildren();
                slideIds.Append(slides.AsEnumerable().Reverse());
            }
            source.Position = 0;

            TextExtractionResult result = await new PresentationTextExtractor(
                NullLogger<PresentationTextExtractor>.Instance).ExtractAsync(source);

            Assert.That(result, Is.EqualTo(new TextExtractionResult(string.Join(
                Environment.NewLine, "Second slide", "First slide", "Заметки докладчика"), false)));
        }

        [Test]
        public async Task PresentationExtractor_ReadsNonSeekableStreamAndLeavesSourceOpen()
        {
            using MemoryStream presentation = CreatePresentation("Slide text");
            using MemoryStream compressed = new();
            await using (GZipStream writer = new(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await presentation.CopyToAsync(writer);
            }
            compressed.Position = 0;
            using GZipStream source = new(compressed, CompressionMode.Decompress);

            TextExtractionResult result = await new PresentationTextExtractor(
                NullLogger<PresentationTextExtractor>.Instance).ExtractAsync(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(new TextExtractionResult("Slide text", false)));
                Assert.That(source.CanRead, Is.True);
            });
        }

        private static Shape CreatePresentationTextShape(string text) => new(new TextBody(
            new Drawing.BodyProperties(), new Drawing.ListStyle(),
            new Drawing.Paragraph(new Drawing.Run(new Drawing.Text(text)))));
    }
}
