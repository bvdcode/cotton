// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Services;
using Cotton.Shared;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.Handlers.Files
{
    public class ShareFileQuery(string token, string? view, HttpRequest httpRequest) : IRequest<ShareFileResult>
    {
        public string Token { get; } = token;
        public string? View { get; } = view;
        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    public class ShareFileQueryHandler(
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        IHttpContextAccessor _httpContextAccessor,
        IStoragePipeline _storage) : IRequestHandler<ShareFileQuery, ShareFileResult>
    {
        public async Task<ShareFileResult> Handle(ShareFileQuery request, CancellationToken ct)
        {
            bool isHead = HttpMethods.IsHead(request.HttpRequest.Method);
            DateTime now = DateTime.UtcNow;

            string mode = (request.View ?? "page").Trim().ToLowerInvariant();
            if (request.View is not null && mode is not ("page" or "download" or "inline"))
            {
                return ShareFileResult.AsBadRequest("Invalid view mode. Valid values: page, download, inline.");
            }

            bool isHtml = mode == "page";
            bool isInlineFile = mode == "inline";

            string baseAppUrl = $"{request.HttpRequest.Scheme}://{request.HttpRequest.Host}";

            IQueryable<DownloadToken> query = _dbContext.DownloadTokens
                .Where(x => x.Token == request.Token && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .Include(x => x.NodeFile)
                .ThenInclude(x => x.FileManifest)
                .Include(x => x.NodeFile)
                .ThenInclude(x => x.Node)
                .AsQueryable();

            if (!isHtml && !isHead)
            {
                query = query
                    .Include(x => x.NodeFile)
                    .ThenInclude(x => x.FileManifest)
                    .ThenInclude(x => x.FileManifestChunks)
                    .ThenInclude(x => x.Chunk);
            }

            var downloadToken = await query.FirstOrDefaultAsync(cancellationToken: ct);
            if (downloadToken == null || downloadToken.NodeFile.Node.Type != NodeType.Default)
            {
                return isHtml
                    ? ShareFileResult.AsRedirect($"{baseAppUrl}/404")
                    : ShareFileResult.AsNotFound("File not found");
            }

            var file = downloadToken.NodeFile.FileManifest;
            if (isHtml)
            {
                string canonicalUrl = $"{baseAppUrl}/s/{request.Token}";
                string appShareUrl = $"{baseAppUrl}/share/{request.Token}";

                string? hex = (file.EncryptedFilePreviewHash == null || file.EncryptedFilePreviewHash.Length == 0)
                    ? null
                    : Convert.ToHexString(file.EncryptedFilePreviewHash);

                string previewTag = hex == null
                    ? string.Empty
                    : ($"<meta property=\"og:image\" content=\"{WebUtility.HtmlEncode($"{baseAppUrl}{Routes.V1.Previews}/{hex}.webp")}\" />\n" +
                       $"<meta name=\"twitter:image\" content=\"{WebUtility.HtmlEncode($"{baseAppUrl}{Routes.V1.Previews}/{hex}.webp")}\" />");

                string html = $"""
                <!doctype html>
                <html lang=\"en\">
                <head>
                  <meta charset=\"utf-8\">
                  <title>{WebUtility.HtmlEncode(downloadToken.FileName)} – Cotton</title>
                  <meta http-equiv=\"refresh\" content=\"0;url={WebUtility.HtmlEncode(appShareUrl)}\" />
                  <link rel=\"canonical\" href=\"{WebUtility.HtmlEncode(canonicalUrl)}\" />
                  <meta property=\"og:site_name\" content=\"Cotton Cloud\" />
                  <meta property=\"og:title\" content=\"{WebUtility.HtmlEncode(downloadToken.FileName)}\" />
                  <meta property=\"og:description\" content=\"Shared via Cotton Cloud\" />
                  <meta property=\"og:type\" content=\"website\" />
                  <meta property=\"og:url\" content=\"{WebUtility.HtmlEncode(canonicalUrl)}\" />
                  {previewTag}
                  <meta name=\"twitter:card\" content=\"summary_large_image\" />
                </head>
                <body>
                  <noscript>
                    <p><a href=\"{WebUtility.HtmlEncode(appShareUrl)}\">Continue</a></p>
                  </noscript>
                  <script>
                    window.location.replace({JsonSerializer.Serialize(appShareUrl)});
                  </script>
                </body>
                </html>
                """;

                return ShareFileResult.AsHtml(html);
            }

            var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(file.ProposedContentHash)}\"");
            var lastModified = new DateTimeOffset(downloadToken.CreatedAt);

            if (isHead)
            {
                return ShareFileResult.AsHead(
                    contentType: file.ContentType,
                    contentLength: file.SizeBytes,
                    entityTag: entityTag.ToString(),
                    fileName: downloadToken.FileName,
                    inline: isInlineFile);
            }

            string[] uids = file.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = file.SizeBytes,
                ChunkLengths = file.FileManifestChunks.GetChunkLengths()
            };
            Stream stream = _storage.GetBlobStream(uids, context);
            string? downloadName = isInlineFile ? null : downloadToken.FileName;
            if (_httpContextAccessor.HttpContext != null)
            {
                await _notifications.SendSharedFileDownloadedNotification(
                    downloadToken.NodeFile.OwnerId,
                    _httpContextAccessor.HttpContext.Request.GetRemoteIPAddress(),
                    _httpContextAccessor.HttpContext.Request.Headers.UserAgent);
            }
            return ShareFileResult.AsStream(
                stream: stream,
                contentType: file.ContentType,
                downloadName: downloadName,
                lastModified: lastModified,
                entityTag: entityTag,
                deleteAfterUse: downloadToken.DeleteAfterUse,
                deleteTokenId: downloadToken.Id);
        }
    }

    public sealed record ShareFileResult
    {
        public string Kind { get; init; } = "";
        public string? RedirectUrl { get; init; }
        public string? HtmlContent { get; init; }

        public string? ContentType { get; init; }
        public long? ContentLength { get; init; }
        public string? EntityTag { get; init; }
        public string? FileName { get; init; }
        public bool? Inline { get; init; }

        public Stream? FileStream { get; init; }
        public string? DownloadName { get; init; }
        public DateTimeOffset? LastModified { get; init; }
        public EntityTagHeaderValue? EntityTagValue { get; init; }
        public bool DeleteAfterUse { get; init; }
        public Guid? DeleteTokenId { get; init; }

        public string? ErrorMessage { get; init; }

        public static ShareFileResult AsBadRequest(string message) => new() { Kind = "badRequest", ErrorMessage = message };
        public static ShareFileResult AsNotFound(string message) => new() { Kind = "notFound", ErrorMessage = message };
        public static ShareFileResult AsRedirect(string url) => new() { Kind = "redirect", RedirectUrl = url };
        public static ShareFileResult AsHtml(string html) => new() { Kind = "html", HtmlContent = html };
        public static ShareFileResult AsHead(string contentType, long contentLength, string entityTag, string fileName, bool inline) =>
            new()
            {
                Kind = "head",
                ContentType = contentType,
                ContentLength = contentLength,
                EntityTag = entityTag,
                FileName = fileName,
                Inline = inline,
            };

        public static ShareFileResult AsStream(Stream stream, string contentType, string? downloadName, DateTimeOffset lastModified, EntityTagHeaderValue entityTag, bool deleteAfterUse, Guid deleteTokenId) =>
            new()
            {
                Kind = "stream",
                FileStream = stream,
                ContentType = contentType,
                DownloadName = downloadName,
                LastModified = lastModified,
                EntityTagValue = entityTag,
                DeleteAfterUse = deleteAfterUse,
                DeleteTokenId = deleteTokenId,
            };
    }
}
