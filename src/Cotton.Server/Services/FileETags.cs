// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Builds and evaluates file content ETags.
    /// </summary>
    public static class FileETags
    {
        private const string ETagPrefix = "sha256-";

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
            if (ifMatchHeader is null)
            {
                return true;
            }

            if (ifMatchHeader == "*")
            {
                return true;
            }

            string currentETag = GetQuotedContentETag(nodeFile);
            if (string.Equals(ifMatchHeader, currentETag, StringComparison.Ordinal))
            {
                return true;
            }

            if (!EntityTagHeaderValue.TryParseStrictList(
                [ifMatchHeader],
                out IList<EntityTagHeaderValue>? clientEntityTags))
            {
                return false;
            }

            EntityTagHeaderValue currentEntityTag = EntityTagHeaderValue.Parse(currentETag);
            return clientEntityTags.Any(clientEntityTag =>
                EntityTagHeaderValue.Any.Equals(clientEntityTag)
                || clientEntityTag.Compare(currentEntityTag, useStrongComparison: true));
        }

        /// <summary>
        /// Reads the If-Match header from a request.
        /// </summary>
        public static string? ReadIfMatch(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.Headers.TryGetValue(HeaderNames.IfMatch, out StringValues value) ? value.ToString() : null;
        }

        /// <summary>
        /// Determines whether the request's If-None-Match condition matches the current entity tag.
        /// </summary>
        public static bool MatchesIfNoneMatchHeader(HttpRequest request, EntityTagHeaderValue entityTag)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(entityTag);

            if (!request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out StringValues values))
            {
                return false;
            }

            IList<EntityTagHeaderValue> clientEntityTags = EntityTagHeaderValue.ParseList([.. values!]);
            return clientEntityTags.Any(clientEntityTag =>
                EntityTagHeaderValue.Any.Equals(clientEntityTag)
                || clientEntityTag.Compare(entityTag, useStrongComparison: false));
        }

        private static string QuoteETag(string value)
        {
            return $"\"{value}\"";
        }
    }
}
