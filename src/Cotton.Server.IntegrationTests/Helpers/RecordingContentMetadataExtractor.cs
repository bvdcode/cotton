// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services.FileMetadata;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class RecordingContentMetadataExtractor(
        string[] supportedContentTypes,
        Func<Stream, int, IReadOnlyDictionary<string, string>> extract) : IFileContentMetadataExtractor
    {
        public List<string> ContentTypes { get; } = [];

        public bool Supports(string contentType) => supportedContentTypes.Contains(contentType);

        public Task<IReadOnlyDictionary<string, string>> ExtractAsync(
            Stream stream, string contentType, CancellationToken cancellationToken)
        {
            ContentTypes.Add(contentType);
            return Task.FromResult(extract(stream, ContentTypes.Count));
        }
    }
}
