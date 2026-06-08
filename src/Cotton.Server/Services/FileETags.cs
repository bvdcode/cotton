// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Builds and evaluates file content ETags.
    /// </summary>
    public static class FileETags
    {
        private const string ETagPrefix = "sha256-";
        private const string WeakETagPrefix = "W/";

        /// <summary>
        /// Gets the current strong file content ETag without quotes.
        /// </summary>
        public static string GetContentETag(NodeFile nodeFile)
        {
            ArgumentNullException.ThrowIfNull(nodeFile);
            return GetContentETag(nodeFile.FileManifest);
        }

        /// <summary>
        /// Gets the current strong file content ETag without quotes.
        /// </summary>
        public static string GetContentETag(FileManifest fileManifest)
        {
            ArgumentNullException.ThrowIfNull(fileManifest);
            return ETagPrefix + Hasher.ToHexStringHash(fileManifest.ProposedContentHash);
        }

        /// <summary>
        /// Gets the current strong file content ETag with HTTP quotes.
        /// </summary>
        public static string GetQuotedContentETag(NodeFile nodeFile)
        {
            return QuoteETag(GetContentETag(nodeFile));
        }

        /// <summary>
        /// Gets the current strong file content ETag with HTTP quotes.
        /// </summary>
        public static string GetQuotedContentETag(FileManifest fileManifest)
        {
            return QuoteETag(GetContentETag(fileManifest));
        }

        /// <summary>
        /// Creates an HTTP entity tag for the current file content.
        /// </summary>
        public static EntityTagHeaderValue CreateContentEntityTag(NodeFile nodeFile)
        {
            return EntityTagHeaderValue.Parse(GetQuotedContentETag(nodeFile));
        }

        /// <summary>
        /// Creates an HTTP entity tag for the current file content.
        /// </summary>
        public static EntityTagHeaderValue CreateContentEntityTag(FileManifest fileManifest)
        {
            return EntityTagHeaderValue.Parse(GetQuotedContentETag(fileManifest));
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

            string current = NormalizeStrongETag(GetContentETag(nodeFile));
            foreach (string value in ifMatchHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (value == "*")
                {
                    return true;
                }

                string? normalized = NormalizeStrongETagOrNull(value);
                if (normalized is not null && string.Equals(normalized, current, StringComparison.Ordinal))
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

        private static string QuoteETag(string value)
        {
            return $"\"{value}\"";
        }

        private static string NormalizeStrongETag(string value)
        {
            return value.Trim().Trim('"');
        }

        private static string? NormalizeStrongETagOrNull(string value)
        {
            string normalized = value.Trim();
            if (normalized.StartsWith(WeakETagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return NormalizeStrongETag(normalized);
        }
    }
}
