// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Represents a local sync root scan with directories and files.
/// </summary>
public sealed class LocalTreeSnapshot
{
    /// <summary>
    /// Gets discovered child directories under the root.
    /// </summary>
    public List<LocalDirectorySnapshot> Directories { get; set; } = [];

    /// <summary>
    /// Gets discovered files under the root.
    /// </summary>
    public List<LocalFileSnapshot> Files { get; set; } = [];
}
