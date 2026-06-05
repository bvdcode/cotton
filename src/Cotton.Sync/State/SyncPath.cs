// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text;

namespace Cotton.Sync.State;

/// <summary>
/// Normalizes relative paths used by the sync state store.
/// </summary>
public static class SyncPath
{
    private const int MaximumPortableRelativePathLength = 32767;
    private const int MaximumPathSegmentLength = 255;
    private static readonly char[] InvalidWindowsSegmentCharacters = ['<', '>', ':', '"', '|', '?', '*'];
    private static readonly HashSet<string> ReservedWindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    /// <summary>
    /// Normalizes a relative path to the display form stored in sync state.
    /// </summary>
    public static string Normalize(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string slashNormalized = relativePath.Replace('\\', '/');
        if (slashNormalized[0] == '/')
        {
            throw new SyncPathValidationException(
                relativePath,
                null,
                "relative path cannot be rooted.");
        }

        string normalized = slashNormalized.Trim('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Split('/').Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Relative path must contain non-empty segments.", nameof(relativePath));
        }

        normalized = normalized.Normalize(NormalizationForm.FormC);
        ValidatePortablePath(relativePath, normalized);
        return normalized;
    }

    /// <summary>
    /// Builds the case-insensitive storage key for a relative path.
    /// </summary>
    public static string ToKey(string relativePath)
    {
        return Normalize(relativePath).ToUpperInvariant();
    }

    private static void ValidatePortablePath(string originalPath, string normalizedPath)
    {
        if (normalizedPath.Length > MaximumPortableRelativePathLength)
        {
            throw new SyncPathValidationException(
                originalPath,
                null,
                "relative path length exceeds " + MaximumPortableRelativePathLength + " characters.");
        }

        foreach (string segment in normalizedPath.Split('/'))
        {
            ValidatePortableSegment(originalPath, segment);
        }
    }

    private static void ValidatePortableSegment(string originalPath, string segment)
    {
        if (segment.Length > MaximumPathSegmentLength)
        {
            throw new SyncPathValidationException(
                originalPath,
                segment,
                "path segment length exceeds " + MaximumPathSegmentLength + " characters.");
        }

        if (segment[^1] == '.' || segment[^1] == ' ')
        {
            throw new SyncPathValidationException(
                originalPath,
                segment,
                "Windows paths cannot end a segment with a space or dot.");
        }

        if (segment.Any(static character => char.IsControl(character) || InvalidWindowsSegmentCharacters.Contains(character)))
        {
            throw new SyncPathValidationException(
                originalPath,
                segment,
                "Windows paths cannot contain control characters or reserved characters.");
        }

        int extensionSeparatorIndex = segment.IndexOf('.');
        string deviceName = extensionSeparatorIndex < 0 ? segment : segment[..extensionSeparatorIndex];
        if (ReservedWindowsDeviceNames.Contains(deviceName))
        {
            throw new SyncPathValidationException(
                originalPath,
                segment,
                "Windows reserves the device name '" + deviceName + "'.");
        }
    }
}
