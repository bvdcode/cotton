// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Chunks;

/// <summary>
/// Provides chunk upload and deduplication operations.
/// </summary>
public interface ICottonChunkClient
{
    /// <summary>
    /// Checks whether a chunk already exists for the user.
    /// </summary>
    Task<bool> ExistsAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads one raw chunk body.
    /// </summary>
    Task UploadRawAsync(string hash, Stream content, string contentType = "application/octet-stream", CancellationToken cancellationToken = default);
}
