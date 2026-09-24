// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.Logging;
using System.IO.Packaging;
using DrawingText = DocumentFormat.OpenXml.Drawing.Text;

namespace Cotton.TextExtraction
{
    public class PresentationTextExtractor(ILogger<PresentationTextExtractor> logger) : FileTextExtractor
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
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
                foreach (SlideId slideId in slideIds.Elements<SlideId>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relationshipId = slideId.RelationshipId?.Value
                        ?? throw new FileFormatException("A slide has no relationship id.");
                    SlidePart slidePart = (SlidePart)presentationPart.GetPartById(relationshipId);
                    Slide slide = slidePart.Slide
                        ?? throw new FileFormatException("A slide is empty.");
                    AppendParagraphs(slide, text, cancellationToken);
                    if (text.IsTruncated)
                    {
                        return;
                    }
                    if (slidePart.NotesSlidePart?.NotesSlide is not null)
                    {
                        AppendParagraphs(slidePart.NotesSlidePart.NotesSlide, text, cancellationToken);
                        if (text.IsTruncated)
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException)
            {
                logger.LogWarning(ex, "Unable to extract presentation text.");
                throw new FileTextExtractionException("Unable to read presentation text.", ex);
            }
        }

        private static void AppendParagraphs(DocumentFormat.OpenXml.OpenXmlElement root,
            TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            foreach (DocumentFormat.OpenXml.Drawing.Paragraph paragraph in
                root.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (DrawingText run in paragraph.Descendants<DrawingText>())
                {
                    text.AppendNormalized(run.Text);
                    if (text.IsTruncated)
                    {
                        return;
                    }
                }
                text.AppendLineBreak();
            }
        }
    }
}
