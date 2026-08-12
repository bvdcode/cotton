// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using System.Net;

namespace Cotton.Server.Helpers
{
    public static class RequestBaseUrlHelpers
    {
        private const string ForwardedProtoHeader = "X-Forwarded-Proto";

        public static string GetBaseUrl(
            HttpRequest request,
            IPAddress? trustedProxyIpAddress = null,
            byte? trustedProxyPrefixLength = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            string scheme = request.Scheme;
            if (request.CanTrustForwardedHeaders(
                    trustedProxyIpAddress,
                    trustedProxyPrefixLength))
            {
                scheme = GetForwardedScheme(request) ?? scheme;
            }

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
