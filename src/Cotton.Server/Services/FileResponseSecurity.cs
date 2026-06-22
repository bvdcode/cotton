// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Applies browser-facing safety rules for user-controlled file content.
    /// </summary>
    public static class FileResponseSecurity
    {
        private const string BinaryDownloadContentType = "application/octet-stream";
        private const string NoSniffHeader = "X-Content-Type-Options";
        private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
        private const string SandboxedFileContentPolicy =
            "sandbox; default-src 'none'; script-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'";

        private static readonly ISet<string> DangerousInlineContentTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "text/html",
                "application/xhtml+xml",
                "image/svg+xml",
                "application/svg+xml",
                "text/xml",
                "application/xml",
            };

        /// <summary>
        /// Returns whether the content type can execute active browser content when served inline.
        /// </summary>
        public static bool IsDangerousInlineContentType(string? contentType)
        {
            string mediaType = NormalizeMediaType(contentType);
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return DangerousInlineContentTypes.Contains(mediaType)
                || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the content type that should be emitted on the HTTP response.
        /// </summary>
        public static string ResolveContentTypeForResponse(string? contentType, bool requestedInline)
        {
            string resolvedContentType = string.IsNullOrWhiteSpace(contentType)
                ? FileManifestService.DefaultContentType
                : contentType;

            return requestedInline && IsDangerousInlineContentType(resolvedContentType)
                ? BinaryDownloadContentType
                : resolvedContentType;
        }

        /// <summary>
        /// Resolves the Content-Disposition filename. A null result allows inline rendering.
        /// </summary>
        public static string? ResolveFileDownloadName(string fileName, bool requestedInline, string? contentType)
        {
            return requestedInline && !IsDangerousInlineContentType(contentType)
                ? null
                : fileName;
        }

        /// <summary>
        /// Resolves the Content-Disposition disposition type for HEAD-style responses.
        /// </summary>
        public static string ResolveContentDispositionType(string? contentType, bool requestedInline)
        {
            return requestedInline && !IsDangerousInlineContentType(contentType)
                ? "inline"
                : "attachment";
        }

        /// <summary>
        /// Adds defensive browser headers to file responses.
        /// </summary>
        public static void ApplyFileResponseHeaders(HttpResponse response, string? originalContentType, bool requestedInline)
        {
            response.Headers[NoSniffHeader] = "nosniff";
            if (requestedInline && IsDangerousInlineContentType(originalContentType))
            {
                response.Headers[ContentSecurityPolicyHeader] = SandboxedFileContentPolicy;
            }
        }

        private static string NormalizeMediaType(string? contentType)
        {
            return string.IsNullOrWhiteSpace(contentType)
                ? string.Empty
                : contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        }
    }
}
