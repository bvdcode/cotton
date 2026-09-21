// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RecordingFileTextExtractor(string[] contentTypes, Func<Stream, int, string> extract) : FileTextExtractor
    {
        public override IEnumerable<string> SupportedContentTypes => contentTypes;

        public int Attempts { get; private set; }

        protected override Task ExtractAsync(Stream source, TextExtractionBuffer text, CancellationToken cancellationToken)
        {
            text.Append(extract(source, ++Attempts));
            return Task.CompletedTask;
        }
    }
}
