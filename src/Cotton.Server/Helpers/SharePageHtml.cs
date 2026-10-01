// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Net;
using System.Text.Json;

namespace Cotton.Server.Helpers
{
    public static class SharePageHtml
    {
        public static string Create(string baseAppUrl, string token, string fileName, bool hasPreview)
        {
            string canonicalUrl = $"{baseAppUrl}/s/{Uri.EscapeDataString(token)}";
            string appShareUrl = $"{baseAppUrl}/share/{Uri.EscapeDataString(token)}";
            string previewUrl = hasPreview
                ? $"{canonicalUrl}/preview.jpg"
                : $"{baseAppUrl}/assets/images/social-preview.jpg";
            return $"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <title>{WebUtility.HtmlEncode(fileName)} - Cotton Cloud</title>
                  <meta http-equiv="refresh" content="0;url={WebUtility.HtmlEncode(appShareUrl)}" />
                  <link rel="canonical" href="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  <meta property="og:site_name" content="Cotton Cloud" />
                  <meta property="og:title" content="{WebUtility.HtmlEncode(fileName)}" />
                  <meta property="og:description" content="Shared via Cotton Cloud" />
                  <meta property="og:type" content="website" />
                  <meta property="og:url" content="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  <meta property="og:image" content="{WebUtility.HtmlEncode(previewUrl)}" />
                  <meta property="og:image:type" content="image/jpeg" />
                  <meta property="og:image:alt" content="{WebUtility.HtmlEncode(fileName)}" />
                  <meta name="twitter:image" content="{WebUtility.HtmlEncode(previewUrl)}" />
                  <meta name="twitter:card" content="summary_large_image" />
                </head>
                <body>
                  <noscript>
                    <p><a href="{WebUtility.HtmlEncode(appShareUrl)}">Continue</a></p>
                  </noscript>
                  <script>
                    window.location.replace({JsonSerializer.Serialize(appShareUrl)});
                  </script>
                </body>
                </html>
                """;
        }
    }
}
