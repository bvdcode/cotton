// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Extensions;
using Cotton.Server.Services.WebDav;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Query for WebDAV GET operation
/// </summary>
public record WebDavGetFileQuery(
    Guid UserId,
    string Path) : IRequest<WebDavGetFileResult>;

/// <summary>
/// Result of WebDAV GET operation
/// </summary>
public record WebDavGetFileResult(
    bool Found,
    bool IsCollection,
    Stream? Content = null,
    string? ContentType = null,
    long ContentLength = 0,
    string? FileName = null,
    DateTimeOffset? LastModified = null,
    string? ETag = null);

/// <summary>
/// Handler for WebDAV GET operation
/// </summary>
public class WebDavGetFileQueryHandler(
    IWebDavPathResolver _pathResolver,
    IStoragePipeline _storage,
    ILogger<WebDavGetFileQueryHandler> _logger)
    : IRequestHandler<WebDavGetFileQuery, WebDavGetFileResult>
{
    public async Task<WebDavGetFileResult> Handle(WebDavGetFileQuery request, CancellationToken ct)
    {
        var resolveResult = await _pathResolver.ResolvePathAsync(request.UserId, request.Path, ct);

        if (!resolveResult.Found)
        {
            _logger.LogDebug("WebDAV GET: Path not found: {Path}", request.Path);
            return new WebDavGetFileResult(false, false);
        }

        if (resolveResult.IsCollection)
        {
            // Collections don't have content to download
            return new WebDavGetFileResult(true, true);
        }

        var nodeFile = resolveResult.NodeFile!;
        var manifest = nodeFile.FileManifest;

        var chunkHashes = manifest.FileManifestChunks
            .OrderBy(c => c.ChunkOrder)
            .Select(c => Convert.ToHexString(c.ChunkHash).ToLowerInvariant())
            .ToArray();

        var context = new PipelineContext
        {
            FileSizeBytes = manifest.SizeBytes,
            ChunkLengths = manifest.FileManifestChunks.GetChunkLengths()
        };

        var stream = _storage.GetBlobStream(chunkHashes, context);

        return new WebDavGetFileResult(
            Found: true,
            IsCollection: false,
            Content: stream,
            ContentType: manifest.ContentType,
            ContentLength: manifest.SizeBytes,
            FileName: nodeFile.Name,
            LastModified: nodeFile.UpdatedAt,
            ETag: $"\"{nodeFile.Id}:{nodeFile.FileManifestId}\"");
    }
}
