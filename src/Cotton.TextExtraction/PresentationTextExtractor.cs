// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;

namespace Cotton.TextExtraction
{
    public class PresentationTextExtractor(ILogger<PresentationTextExtractor> logger) : IFileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

        public IEnumerable<string> SupportedContentTypes => [ContentType];

        public async Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
                using PresentationDocument document = PresentationDocument.Open(seekable.Stream, false);
                PresentationPart presentationPart = document.PresentationPart
                    ?? throw new FileFormatException("The presentation has no main part.");
                Presentation presentation = presentationPart.Presentation
                    ?? throw new FileFormatException("The presentation root is missing.");
                SlideIdList slideIds = presentation.SlideIdList
                    ?? throw new FileFormatException("The presentation has no slide list.");
                List<string> lines = [];
                foreach (SlideId slideId in slideIds.Elements<SlideId>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relationshipId = slideId.RelationshipId?.Value
                        ?? throw new FileFormatException("A slide has no relationship id.");
                    SlidePart slidePart = (SlidePart)presentationPart.GetPartById(relationshipId);
                    Slide slide = slidePart.Slide
                        ?? throw new FileFormatException("A slide is empty.");
                    lines.AddRange(slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>()
                        .Select(paragraph => string.Concat(paragraph.Descendants<DrawingText>()
                            .Select(text => text.Text))));
                    if (slidePart.NotesSlidePart?.NotesSlide is not null)
                    {
                        lines.AddRange(slidePart.NotesSlidePart.NotesSlide
                            .Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>()
                            .Select(paragraph => string.Concat(paragraph.Descendants<DrawingText>()
                                .Select(text => text.Text))));
                    }
                }
                return TextExtractionUtilities.JoinLines(lines);
            }
            catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to extract presentation text.");
                throw new FileTextExtractionException("Unable to read presentation text.", ex);
            }
        }
    }
}
