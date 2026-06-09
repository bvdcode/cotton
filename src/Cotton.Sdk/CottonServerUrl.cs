// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk
{
    /// <summary>
    /// Normalizes Cotton server URLs for SDK clients and desktop tooling.
    /// </summary>
    public static class CottonServerUrl
    {
        private const string DefaultScheme = "https://";

        /// <summary>
        /// Normalizes an optional server URL, accepting bare hosts as HTTPS URLs.
        /// </summary>
        public static Uri? NormalizeOptional(string? value)
        {
            string? normalized = NormalizeOptionalText(value);
            return normalized is null ? null : TryCreate(normalized);
        }

        /// <summary>
        /// Normalizes a required server URL, accepting bare hosts as HTTPS URLs.
        /// </summary>
        public static Uri NormalizeRequired(string value, string parameterName)
        {
            string? normalized = NormalizeOptionalText(value);
            if (normalized is null)
            {
                throw new ArgumentException("Server URL is required.", parameterName);
            }

            Uri? uri = TryCreate(normalized);
            if (uri is null)
            {
                throw new ArgumentException("Server URL must be an HTTP or HTTPS URL.", parameterName);
            }

            return uri;
        }

        private static Uri? TryCreate(string value)
        {
            if (TryCreateHttpUri(value, out Uri? uri))
            {
                return uri;
            }

            if (value.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            return TryCreateHttpUri(DefaultScheme + value, out uri) ? uri : null;
        }

        private static bool TryCreateHttpUri(string value, out Uri? uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && IsHttpScheme(uri);
        }

        private static bool IsHttpScheme(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static string? NormalizeOptionalText(string? value)
        {
            string? normalized = value?.Trim();
            return string.IsNullOrEmpty(normalized) ? null : normalized;
        }
    }
}
