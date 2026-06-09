// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Internal;

internal static class CottonRouteUri
{
    public static Uri Create(Uri baseAddress, string path)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absoluteUri)
            && IsHttpUri(absoluteUri))
        {
            return absoluteUri;
        }

        string relative = path.TrimStart('/');
        int queryIndex = relative.IndexOf('?', StringComparison.Ordinal);
        string relativePath = queryIndex >= 0
            ? relative[..queryIndex]
            : relative;
        string query = queryIndex >= 0
            ? relative[(queryIndex + 1)..]
            : string.Empty;
        string basePath = baseAddress.AbsolutePath.TrimEnd('/');
        string combinedPath = string.IsNullOrEmpty(basePath)
            ? "/" + relativePath
            : basePath + "/" + relativePath;

        var builder = new UriBuilder(baseAddress)
        {
            Path = combinedPath,
            Query = query,
        };
        return builder.Uri;
    }

    private static bool IsHttpUri(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
