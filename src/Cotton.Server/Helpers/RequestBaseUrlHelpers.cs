// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Helpers
{
    /// <summary>
    /// Builds externally visible request URLs without changing connection-level request metadata.
    /// </summary>
    public static class RequestBaseUrlHelpers
    {
        private const string ForwardedProtoHeader = "X-Forwarded-Proto";

        /// <summary>
        /// Gets request base URL, honoring proxy-forwarded scheme for URL generation only.
        /// </summary>
        public static string GetBaseUrl(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            string scheme = GetForwardedScheme(request) ?? request.Scheme;
            return $"{scheme}://{request.Host.Value}".TrimEnd('/');
        }

        private static string? GetForwardedScheme(HttpRequest request)
        {
            string? rawValue = request.Headers[ForwardedProtoHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            int commaIndex = rawValue.IndexOf(',', StringComparison.Ordinal);
            string value = (commaIndex >= 0 ? rawValue[..commaIndex] : rawValue).Trim();
            if (value.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UriSchemeHttps;
            }

            return value.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                ? Uri.UriSchemeHttp
                : null;
        }
    }
}
