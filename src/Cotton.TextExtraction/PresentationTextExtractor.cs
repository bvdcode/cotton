// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Mediana.Documents.Presentation;
using Microsoft.Extensions.Logging;

namespace Cotton.TextExtraction
{
    public class PresentationTextExtractor(ILogger<PresentationTextExtractor> logger) : DocumentTextExtractor(logger)
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        private const int MaximumSourceBytes = int.MaxValue;
        private const int MaximumPartBytes = 1024 * 1024 * 1024;
        private const long MaximumUncompressedBytes = 4L * 1024 * 1024 * 1024;

        public override IEnumerable<string> SupportedContentTypes => [ContentType];

        protected override async Task ReadTextAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            await using SeekableReadStream seekable = await SeekableReadStream.OpenAsync(source, cancellationToken);
            using PresentationDocument document = PresentationReader.Open(seekable.Stream, new()
            {
                Package = new()
                {
                    MaximumInputBytes = MaximumSourceBytes,
                    MaximumPartBytes = MaximumPartBytes,
                    MaximumUncompressedBytes = MaximumUncompressedBytes,
                },
            }, leaveOpen: true, cancellationToken: cancellationToken);
            text.AppendLines(document.ReadText(new() { IncludeSpeakerNotes = true }, cancellationToken)
                .Select(block => block.Text), cancellationToken);
        }
    }
}
