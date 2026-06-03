// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Represents a local file that could not be scanned safely.
/// </summary>
public sealed class LocalFileUnavailableException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileUnavailableException" /> class.
    /// </summary>
    public LocalFileUnavailableException(string relativePath, string fullPath, Exception innerException)
        : base($"Local file '{relativePath}' could not be scanned safely.", innerException)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileUnavailableException" /> class.
    /// </summary>
    public LocalFileUnavailableException(string relativePath, string fullPath, string reason)
        : base($"Local file '{relativePath}' could not be scanned safely: {reason}")
    {
        RelativePath = relativePath;
        FullPath = fullPath;
    }

    /// <summary>
    /// Gets the relative path that could not be scanned.
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the absolute file path that could not be scanned.
    /// </summary>
    public string FullPath { get; }
}
