// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.State;

/// <summary>
/// Normalizes relative paths used by the sync state store.
/// </summary>
public static class SyncPath
{
    /// <summary>
    /// Normalizes a relative path to the display form stored in sync state.
    /// </summary>
    public static string Normalize(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Split('/').Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Relative path must contain non-empty segments.", nameof(relativePath));
        }

        return normalized;
    }

    /// <summary>
    /// Builds the case-insensitive storage key for a relative path.
    /// </summary>
    public static string ToKey(string relativePath)
    {
        return Normalize(relativePath).ToUpperInvariant();
    }
}
