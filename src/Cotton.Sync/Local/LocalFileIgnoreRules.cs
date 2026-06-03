// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Local;

/// <summary>
/// Identifies local files that should not enter the synchronization model.
/// </summary>
public static class LocalFileIgnoreRules
{
    private const string MetadataDirectoryName = ".cotton-sync";

    private static readonly string[] TemporaryFilePrefixes =
    [
        "~$",
        ".#",
    ];

    private static readonly string[] TemporaryFileSuffixes =
    [
        "~",
        ".tmp",
        ".temp",
        ".partial",
        ".part",
        ".crdownload",
        ".download",
        ".swp",
        ".swo",
        ".swn",
    ];

    private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store",
        "Thumbs.db",
        "desktop.ini",
    };

    /// <summary>
    /// Returns whether the relative path should be skipped by local scanning.
    /// </summary>
    public static bool ShouldIgnore(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => string.Equals(segment, MetadataDirectoryName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string fileName = segments[^1];
        return IgnoredFileNames.Contains(fileName)
            || TemporaryFilePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.Ordinal))
            || TemporaryFileSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
