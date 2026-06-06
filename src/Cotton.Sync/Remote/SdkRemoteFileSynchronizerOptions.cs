// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote;

/// <summary>
/// Defines SDK-backed remote file synchronization transfer options.
/// </summary>
public sealed class SdkRemoteFileSynchronizerOptions
{
    /// <summary>
    /// Gets or sets an optional chunk size override. When omitted, the server setting is used.
    /// </summary>
    public int? ChunkSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the page size used when searching or creating remote parent folders.
    /// </summary>
    public int DirectoryPageSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of chunk existence/upload requests running at the same time.
    /// File reads stay sequential; this limit only controls network work already buffered in memory.
    /// </summary>
    public int MaxConcurrentChunkUploads { get; set; } = 3;

    /// <summary>
    /// Gets or sets an optional content type resolver for uploaded files.
    /// </summary>
    public Func<string, string>? ContentTypeResolver { get; set; }
}
