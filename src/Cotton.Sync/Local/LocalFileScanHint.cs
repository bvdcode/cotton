// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Provides a reusable local file hash when file metadata still matches the sync baseline.
/// </summary>
public sealed class LocalFileScanHint
{
    /// <summary>
    /// Gets or sets the known content hash.
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the known file size.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the known local write timestamp.
    /// </summary>
    public DateTime LastWriteUtc { get; set; }
}
