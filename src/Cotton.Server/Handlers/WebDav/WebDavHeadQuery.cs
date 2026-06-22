// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav
{
    /// <summary>
    /// Query for WebDAV HEAD operation
    /// </summary>
    public record WebDavHeadQuery(
        Guid UserId,
        string Path) : IRequest<WebDavHeadResult>;

    /// <summary>
    /// Result of WebDAV HEAD operation
    /// </summary>
    public record WebDavHeadResult(
        bool Found,
        bool IsCollection,
        string? ContentType = null,
        long ContentLength = 0,
        DateTimeOffset? LastModified = null,
        string? ETag = null);

    /// <summary>
    /// Handler for WebDAV HEAD operation
    /// </summary>
    public class WebDavHeadQueryHandler(
        IWebDavPathResolver _pathResolver,
        ILogger<WebDavHeadQueryHandler> _logger)
        : IRequestHandler<WebDavHeadQuery, WebDavHeadResult>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public async Task<WebDavHeadResult> Handle(WebDavHeadQuery request, CancellationToken ct)
        {
            WebDavResolveResult resolveResult = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);

            if (!resolveResult.Found)
            {
                _logger.LogDebug("WebDAV HEAD: Path not found: {Path}", request.Path);
                return new WebDavHeadResult(false, false);
            }

            if (resolveResult.IsCollection && resolveResult.Node is not null)
            {
                return new WebDavHeadResult(
                    Found: true,
                    IsCollection: true,
                    ContentType: "httpd/unix-directory",
                    ContentLength: 0,
                    LastModified: resolveResult.Node.UpdatedAt,
                    ETag: $"\"{resolveResult.Node.Id}\"");
            }

            if (resolveResult.NodeFile is not null)
            {
                FileManifest manifest = resolveResult.NodeFile.FileManifest;
                return new WebDavHeadResult(
                    Found: true,
                    IsCollection: false,
                    ContentType: manifest.ContentType,
                    ContentLength: manifest.SizeBytes,
                    LastModified: resolveResult.NodeFile.UpdatedAt,
                    ETag: FileETags.GetQuotedContentETag(manifest));
            }

            return new WebDavHeadResult(false, false);
        }
    }
}
