// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.Handlers.Folders
{
    public class ShareFolderQuery(string token, string? view, HttpRequest httpRequest) : IRequest<ShareFolderResult>
    {
        public string Token { get; } = token;
        public string? View { get; } = view;
        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    public class ShareFolderQueryHandler(CottonDbContext _dbContext) : IRequestHandler<ShareFolderQuery, ShareFolderResult>
    {
        public async Task<ShareFolderResult> Handle(ShareFolderQuery request, CancellationToken ct)
        {
            string mode = (request.View ?? "page").Trim().ToLowerInvariant();
            if (request.View is not null && mode is not "page")
            {
                return ShareFolderResult.AsBadRequest("Invalid view mode for shared folder. Valid value: page.");
            }

            string baseAppUrl = $"{request.HttpRequest.Scheme}://{request.HttpRequest.Host}";
            DateTime now = DateTime.UtcNow;

            var shareToken = await _dbContext.NodeShareTokens
                .AsNoTracking()
                .Where(x => x.Token == request.Token
                    && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .Include(x => x.Node)
                .FirstOrDefaultAsync(cancellationToken: ct);

            if (shareToken is null || shareToken.Node.Type != NodeType.Default)
            {
                return ShareFolderResult.AsNotFound("Folder not found");
            }

            string html = BuildRedirectHtml(baseAppUrl, request.Token, shareToken.Name);
            return ShareFolderResult.AsHtml(html);
        }

        private static string BuildRedirectHtml(string baseAppUrl, string token, string folderName)
        {
            string canonicalUrl = $"{baseAppUrl}/s/{token}";
            string appShareUrl = $"{baseAppUrl}/share/folder/{token}";
            string previewUrl = $"{baseAppUrl}/assets/images/social-preview.jpg";

            return $"""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <title>{WebUtility.HtmlEncode(folderName)} - Cotton Cloud</title>
                  <meta http-equiv="refresh" content="0;url={WebUtility.HtmlEncode(appShareUrl)}" />
                  <link rel="canonical" href="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  <meta property="og:site_name" content="Cotton Cloud" />
                  <meta property="og:title" content="{WebUtility.HtmlEncode(folderName)}" />
                  <meta property="og:description" content="Shared folder via Cotton Cloud" />
                  <meta property="og:type" content="website" />
                  <meta property="og:url" content="{WebUtility.HtmlEncode(canonicalUrl)}" />
                  <meta property="og:image" content="{WebUtility.HtmlEncode(previewUrl)}" />
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

    /// <summary>
    /// Result type for the <see cref="ShareFolderQuery"/> handler.
    /// </summary>
    public sealed record ShareFolderResult
    {
        public string Kind { get; init; } = "";
        public string? HtmlContent { get; init; }
        public string? ErrorMessage { get; init; }

        public static ShareFolderResult AsBadRequest(string message) => new() { Kind = "badRequest", ErrorMessage = message };
        public static ShareFolderResult AsNotFound(string message) => new() { Kind = "notFound", ErrorMessage = message };
        public static ShareFolderResult AsHtml(string html) => new() { Kind = "html", HtmlContent = html };
    }
}
