// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.FileMetadata
{
    /// <summary>
    /// Extracts deterministic content metadata from immutable file payloads.
    /// </summary>
    public interface IFileContentMetadataExtractor
    {
        bool Supports(string contentType);

        Task<IReadOnlyDictionary<string, string>> ExtractAsync(
            Stream stream,
            string contentType,
            CancellationToken cancellationToken);
    }
}
