// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Internal
{
    internal static class CottonRouteUri
    {
        public static Uri Create(Uri baseAddress, string route)
        {
            ArgumentNullException.ThrowIfNull(baseAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(route);

            if (Uri.TryCreate(route, UriKind.Absolute, out Uri? absoluteUri)
                && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri;
            }

            string baseText = baseAddress.AbsoluteUri;
            if (!baseText.EndsWith("/", StringComparison.Ordinal))
            {
                baseText += "/";
            }

            return new Uri(new Uri(baseText), route.TrimStart('/'));
        }
    }
}
