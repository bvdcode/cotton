// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sdk.Internal;

internal static class CottonRouteUri
{
    public static Uri Create(Uri baseAddress, string path)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri;
        }

        string basePath = baseAddress.AbsolutePath.TrimEnd('/');
        string baseUriText = baseAddress.GetLeftPart(UriPartial.Authority) + basePath + "/";
        return new Uri(new Uri(baseUriText, UriKind.Absolute), path.TrimStart('/'));
    }
}
