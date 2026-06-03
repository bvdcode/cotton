// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using EasyExtensions.Helpers;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Services;

/// <summary>
/// Evaluates file content ETag preconditions for optimistic concurrency.
/// </summary>
public static class FileETagConcurrency
{
    private const string ETagPrefix = "sha256-";

    /// <summary>
    /// Gets the current strong file content ETag without quotes.
    /// </summary>
    public static string GetContentETag(NodeFile nodeFile)
    {
        ArgumentNullException.ThrowIfNull(nodeFile);
        return ETagPrefix + Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash);
    }

    /// <summary>
    /// Determines whether an If-Match header allows mutation of the current file.
    /// </summary>
    public static bool MatchesIfMatchHeader(string? ifMatchHeader, NodeFile nodeFile)
    {
        if (string.IsNullOrWhiteSpace(ifMatchHeader))
        {
            return true;
        }

        string current = Normalize(GetContentETag(nodeFile));
        foreach (string value in ifMatchHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string normalized = Normalize(value);
            if (normalized == "*" || string.Equals(normalized, current, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the If-Match header from a request.
    /// </summary>
    public static string? ReadIfMatch(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Headers.TryGetValue(HeaderNames.IfMatch, out var value) ? value.ToString() : null;
    }

    private static string Normalize(string value)
    {
        string normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..].Trim();
        }

        return normalized.Trim('"');
    }
}
