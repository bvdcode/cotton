// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State;

/// <summary>
/// Identifies paths that should not enter the synchronization model.
/// </summary>
public static class SyncPathIgnoreRules
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
    /// Returns whether the relative path should be skipped by synchronization.
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
        return segments.Any(ShouldIgnoreSegment);
    }

    private static bool ShouldIgnoreSegment(string segment)
    {
        return string.Equals(segment, MetadataDirectoryName, StringComparison.OrdinalIgnoreCase)
            || IgnoredFileNames.Contains(segment)
            || TemporaryFilePrefixes.Any(prefix => segment.StartsWith(prefix, StringComparison.Ordinal))
            || TemporaryFileSuffixes.Any(suffix => segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
