// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Extensions
{
    internal static class NoStoreResponseExtensions
    {
        public static void ApplyNoStoreHeaders(this HttpResponse response)
        {
            response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            response.Headers.Pragma = "no-cache";
            response.Headers.Expires = "0";
        }
    }
}
