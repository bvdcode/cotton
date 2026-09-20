// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.TextExtraction;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RecordingFileTextExtractor(string[] contentTypes, Func<Stream, int, string> extract) : IFileTextExtractor
    {
        public IEnumerable<string> SupportedContentTypes => contentTypes;

        public int Attempts { get; private set; }

        public Task<string> ExtractAsync(Stream source, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(extract(source, ++Attempts));
        }
    }
}
