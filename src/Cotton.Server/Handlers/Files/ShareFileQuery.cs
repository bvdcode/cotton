// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Extensions;
using Cotton.Server.Helpers;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.DatabaseIntegrity;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text.Json;

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Represents a share file query sent through the mediator pipeline.
    /// </summary>
    public class ShareFileQuery(string token, string? view, bool preview, HttpRequest httpRequest) : IRequest<ShareFileResult>
    {
        /// <summary>
        /// Gets the opaque token.
        /// </summary>
        public string Token { get; } = token;
        /// <summary>
        /// Gets the view.
        /// </summary>
        public string? View { get; } = view;
        /// <summary>
        /// Gets whether the request should serve the generated large preview when available.
        /// </summary>
        public bool Preview { get; } = preview;
        /// <summary>
        /// Gets the http request.
        /// </summary>
        public HttpRequest HttpRequest { get; } = httpRequest;
    }

    /// <summary>
    /// Handles share file queries in the mediator pipeline.
    /// </summary>
    public class ShareFileQueryHandler(
        CottonDbContext _dbContext,
        ISharedFileDownloadNotifier _sharedFileDownloadNotifier,
        IHttpContextAccessor _httpContextAccessor,
        IStoragePipeline _storage,
        SettingsProvider _settings,
        IDatabaseIntegrityVerifier _integrity,
        FileGraphIntegrityVerifier _fileGraphIntegrity) : IRequestHandler<ShareFileQuery, ShareFileResult>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<ShareFileResult> Handle(ShareFileQuery request, CancellationToken ct)
        {
            var viewMode = TryParseViewMode(request.View);
            if (viewMode is null)
            {
                return ShareFileResult.AsBadRequest("Invalid view mode. Valid values: page, download, inline.");
            }

            DateTime now = DateTime.UtcNow;
            bool isHead = HttpMethods.IsHead(request.HttpRequest.Method);
            string baseAppUrl = await _settings.GetPublicBaseUrlAsync(ct);
            bool requestsPreview = RequestsInlinePreview(request, viewMode.Value);

            if (viewMode.Value.IsHtml)
            {
                ShareFileResult? nodeShareResult = await TryBuildNodeShareRedirectResultAsync(
                    request.Token,
                    now,
                    baseAppUrl,
                    ct);
                if (nodeShareResult is not null)
                {
                    return nodeShareResult;
                }
            }

            bool includeChunks = !viewMode.Value.IsHtml && !isHead && !requestsPreview;
            var downloadToken = await LoadDownloadTokenAsync(request.Token, now, includeChunks, ct);
            if (downloadToken is null)
            {
                return await BuildMissingTokenResultAsync(request.Token, now, viewMode.Value.IsHtml, baseAppUrl, ct);
            }

            _integrity.RequireValid(_dbContext, downloadToken, "share.download-token");
            if (viewMode.Value.IsHtml || isHead || requestsPreview)
            {
                _fileGraphIntegrity.RequireValidMetadata(_dbContext, downloadToken.NodeFile, "share.file-metadata");
            }
            else
            {
                _fileGraphIntegrity.RequireValidContent(_dbContext, downloadToken.NodeFile, "share.file-download");
            }

            if (downloadToken.NodeFile.Node.Type != NodeType.Default)
            {
                return BuildNotFoundResult(viewMode.Value.IsHtml, baseAppUrl);
            }

            return await BuildDownloadTokenResultAsync(
                request,
                downloadToken,
                viewMode.Value,
                isHead,
                baseAppUrl,
                ct);
        }

        private async Task<DownloadToken?> LoadDownloadTokenAsync(
            string token,
            DateTime now,
            bool includeChunks,
            CancellationToken ct)
        {
            var query = BuildTokenQuery(token, now, includeChunks);
            return await query.FirstOrDefaultAsync(cancellationToken: ct);
        }

        private async Task<ShareFileResult?> TryBuildNodeShareRedirectResultAsync(
            string token,
            DateTime now,
            string baseAppUrl,
            CancellationToken ct)
        {
            var nodeShareToken = await LoadNodeShareTokenAsync(token, now, ct);
            if (nodeShareToken is null)
            {
                return null;
            }

            if (nodeShareToken.Node.Type != NodeType.Default)
            {
                return ShareFileResult.AsRedirect($"{baseAppUrl}/404");
            }

            _integrity.RequireValid(_dbContext, nodeShareToken, "share.node-token");
            return BuildNodeShareRedirect(nodeShareToken, token, baseAppUrl);
        }

        private async Task<ShareFileResult> BuildMissingTokenResultAsync(
            string token,
            DateTime now,
            bool isHtml,
            string baseAppUrl,
            CancellationToken ct)
        {
            if (!isHtml)
            {
                return ShareFileResult.AsNotFound("File not found");
            }

            var nodeShareToken = await LoadNodeShareTokenAsync(token, now, ct);
            if (nodeShareToken is null || nodeShareToken.Node.Type != NodeType.Default)
            {
                return ShareFileResult.AsRedirect($"{baseAppUrl}/404");
            }

            _integrity.RequireValid(_dbContext, nodeShareToken, "share.node-token");
            return BuildNodeShareRedirect(nodeShareToken, token, baseAppUrl);
        }

        private async Task<NodeShareToken?> LoadNodeShareTokenAsync(string token, DateTime now, CancellationToken ct)
        {
            return await _dbContext.NodeShareTokens
                .Include(x => x.Node)
                .Where(x => x.Token == token && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .SingleOrDefaultAsync(cancellationToken: ct);
        }

        private static ShareFileResult BuildNodeShareRedirect(
            NodeShareToken nodeShareToken,
            string token,
            string baseAppUrl)
        {
            string html = BuildRedirectHtml(
                baseAppUrl: baseAppUrl,
                token: token,
                fileName: nodeShareToken.Name,
                previewHashEncryptedHex: null);
            return ShareFileResult.AsHtml(html);
        }

        private static ShareFileResult BuildNotFoundResult(bool isHtml, string baseAppUrl)
        {
            return isHtml
                ? ShareFileResult.AsRedirect($"{baseAppUrl}/404")
                : ShareFileResult.AsNotFound("File not found");
        }

        private async Task<ShareFileResult> BuildDownloadTokenResultAsync(
            ShareFileQuery request,
            DownloadToken downloadToken,
            (bool IsHtml, bool IsInlineFile) viewMode,
            bool isHead,
            string baseAppUrl,
            CancellationToken ct)
        {
            var file = downloadToken.NodeFile.FileManifest;
            if (viewMode.IsHtml)
            {
                return BuildFileRedirect(downloadToken, file, request.Token, baseAppUrl);
            }

            if (RequestsInlinePreview(request, viewMode))
            {
                return CreatePreviewStreamResult(downloadToken, file);
            }

            var entityTag = CreateEntityTag(file);
            if (isHead)
            {
                return ShareFileResult.AsHead(
                    contentType: file.ContentType,
                    contentLength: file.SizeBytes,
                    entityTag: entityTag.ToString(),
                    fileName: downloadToken.FileName,
                    inline: viewMode.IsInlineFile);
            }

            bool isMetadataRangeProbe = IsInlineMetadataRangeProbe(request.HttpRequest, viewMode.IsInlineFile);
            return await CreateStreamResultAsync(
                downloadToken,
                file,
                entityTag,
                new DateTimeOffset(downloadToken.CreatedAt),
                inline: viewMode.IsInlineFile,
                deleteAfterUse: downloadToken.DeleteAfterUse && !isMetadataRangeProbe,
                notifyDownload: !isMetadataRangeProbe,
                ct);
        }

        private static ShareFileResult BuildFileRedirect(
            DownloadToken downloadToken,
            FileManifest file,
            string token,
            string baseAppUrl)
        {
            string html = BuildRedirectHtml(
                baseAppUrl: baseAppUrl,
                token: token,
                fileName: downloadToken.FileName,
                previewHashEncryptedHex: file.GetPreviewHashEncryptedHex());
            return ShareFileResult.AsHtml(html);
        }

        private static (bool IsHtml, bool IsInlineFile)? TryParseViewMode(string? view)
        {
            string mode = (view ?? "page").Trim().ToLowerInvariant();
            if (view is not null && mode is not ("page" or "download" or "inline"))
            {
                return null;
            }

            bool isHtml = mode == "page";
            bool isInlineFile = mode == "inline";
            return (isHtml, isInlineFile);
        }

        private static bool RequestsInlinePreview(
            ShareFileQuery request,
            (bool IsHtml, bool IsInlineFile) viewMode) =>
            request.Preview && viewMode.IsInlineFile && HttpMethods.IsGet(request.HttpRequest.Method);

        private static bool IsInlineMetadataRangeProbe(HttpRequest httpRequest, bool inline)
        {
            if (!inline || !HttpMethods.IsGet(httpRequest.Method))
            {
                return false;
            }

            string? range = httpRequest.Headers[HeaderNames.Range].FirstOrDefault();
            return string.Equals(range?.Trim(), "bytes=0-3", StringComparison.OrdinalIgnoreCase);
        }

        private IQueryable<DownloadToken> BuildTokenQuery(string token, DateTime now, bool includeChunks)
        {
            IQueryable<DownloadToken> query = _dbContext.DownloadTokens
                .Where(x => x.Token == token && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now))
                .Include(x => x.NodeFile)
                .ThenInclude(x => x.FileManifest)
                .Include(x => x.NodeFile)
                .ThenInclude(x => x.Node)
                .AsQueryable();

            if (!includeChunks)
            {
                return query;
            }

            return query
                .Include(x => x.NodeFile)
                .ThenInclude(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk);
        }

        private static EntityTagHeaderValue CreateEntityTag(FileManifest file)
        {
            return FileETags.CreateContentEntityTag(file);
        }

        private ShareFileResult CreatePreviewStreamResult(DownloadToken downloadToken, FileManifest file)
        {
            if (file.LargeFilePreviewHash is null)
            {
                return ShareFileResult.AsNotFound("Preview not found");
            }

            string previewHashHex = Hasher.ToHexStringHash(file.LargeFilePreviewHash);
            var entityTag = new EntityTagHeaderValue($"\"sha256-{previewHashHex}\"");
            Stream previewStream = _storage.GetBlobStream([previewHashHex]);

            return ShareFileResult.AsStream(
                stream: previewStream,
                contentType: "image/webp",
                fileName: downloadToken.FileName,
                downloadName: null,
                lastModified: new DateTimeOffset(downloadToken.CreatedAt),
                entityTag: entityTag,
                deleteAfterUse: false,
                deleteTokenId: downloadToken.Id);
        }

        private static string BuildRedirectHtml(string baseAppUrl,
            string token, string fileName, string? previewHashEncryptedHex)
        {
            string canonicalUrl = $"{baseAppUrl}/s/{token}";
            string appShareUrl = $"{baseAppUrl}/share/{token}";

            string previewUrl = previewHashEncryptedHex is null
                ? $"{baseAppUrl}/assets/images/social-preview.jpg"
                : $"{baseAppUrl}{Routes.V1.Previews}/{previewHashEncryptedHex}.webp";

            string previewTag =
                $"<meta property=\"og:image\" content=\"{WebUtility.HtmlEncode(previewUrl)}\" />\n" +
                $"<meta name=\"twitter:image\" content=\"{WebUtility.HtmlEncode(previewUrl)}\" />";

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
                  {previewTag}
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

        private async Task<ShareFileResult> CreateStreamResultAsync(
            DownloadToken downloadToken,
            FileManifest file,
            EntityTagHeaderValue entityTag,
            DateTimeOffset lastModified,
            bool inline,
            bool deleteAfterUse,
            bool notifyDownload,
            CancellationToken ct)
        {
            string[] uids = file.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = file.SizeBytes,
                ChunkLengths = file.FileManifestChunks.GetChunkLengths()
            };

            Stream stream = _storage.GetBlobStream(uids, context);
            string? downloadName = inline ? null : downloadToken.FileName;

            if (notifyDownload && _httpContextAccessor.HttpContext != null)
            {
                await _sharedFileDownloadNotifier.NotifyOnceAsync(
                    downloadToken.NodeFile.OwnerId,
                    downloadToken.Id,
                    downloadToken.FileName,
                    _httpContextAccessor.HttpContext,
                    ct);
            }

            return ShareFileResult.AsStream(
                stream: stream,
                contentType: file.ContentType,
                fileName: downloadToken.FileName,
                downloadName: downloadName,
                lastModified: lastModified,
                entityTag: entityTag,
                deleteAfterUse: deleteAfterUse,
                deleteTokenId: downloadToken.Id);
        }
    }

    /// <summary>
    /// Represents the result of share file.
    /// </summary>
    public sealed record ShareFileResult
    {
        /// <summary>
        /// Gets or sets the kind.
        /// </summary>
        public string Kind { get; init; } = "";
        /// <summary>
        /// Gets or sets the redirect url.
        /// </summary>
        public string? RedirectUrl { get; init; }
        /// <summary>
        /// Gets or sets the html content.
        /// </summary>
        public string? HtmlContent { get; init; }

        /// <summary>
        /// Gets or sets the response content type.
        /// </summary>
        public string? ContentType { get; init; }
        /// <summary>
        /// Gets or sets the response content length in bytes.
        /// </summary>
        public long? ContentLength { get; init; }
        /// <summary>
        /// Gets or sets the entity tag.
        /// </summary>
        public string? EntityTag { get; init; }
        /// <summary>
        /// Gets or sets the file name shown to clients.
        /// </summary>
        public string? FileName { get; init; }
        /// <summary>
        /// Gets or sets the inline.
        /// </summary>
        public bool? Inline { get; init; }

        /// <summary>
        /// Gets or sets the file stream.
        /// </summary>
        public Stream? FileStream { get; init; }
        /// <summary>
        /// Gets or sets the download name.
        /// </summary>
        public string? DownloadName { get; init; }
        /// <summary>
        /// Gets or sets the last modified.
        /// </summary>
        public DateTimeOffset? LastModified { get; init; }
        /// <summary>
        /// Gets or sets the entity tag value.
        /// </summary>
        public EntityTagHeaderValue? EntityTagValue { get; init; }
        /// <summary>
        /// Deletes after use.
        /// </summary>
        public bool DeleteAfterUse { get; init; }
        /// <summary>
        /// Deletes token id.
        /// </summary>
        public Guid? DeleteTokenId { get; init; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Converts the result to bad request.
        /// </summary>
        public static ShareFileResult AsBadRequest(string message) => new() { Kind = "badRequest", ErrorMessage = message };
        /// <summary>
        /// Converts the result to not found.
        /// </summary>
        public static ShareFileResult AsNotFound(string message) => new() { Kind = "notFound", ErrorMessage = message };
        /// <summary>
        /// Converts the result to redirect.
        /// </summary>
        public static ShareFileResult AsRedirect(string url) => new() { Kind = "redirect", RedirectUrl = url };
        /// <summary>
        /// Converts the result to html.
        /// </summary>
        public static ShareFileResult AsHtml(string html) => new() { Kind = "html", HtmlContent = html };
        /// <summary>
        /// Converts the result to head.
        /// </summary>
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

        /// <summary>
        /// Converts the result to stream.
        /// </summary>
        public static ShareFileResult AsStream(Stream stream, string contentType, string fileName, string? downloadName, DateTimeOffset lastModified, EntityTagHeaderValue entityTag, bool deleteAfterUse, Guid deleteTokenId) =>
            new()
            {
                Kind = "stream",
                FileStream = stream,
                ContentType = contentType,
                FileName = fileName,
                DownloadName = downloadName,
                LastModified = lastModified,
                EntityTagValue = entityTag,
                DeleteAfterUse = deleteAfterUse,
                DeleteTokenId = deleteTokenId,
            };
    }
}
