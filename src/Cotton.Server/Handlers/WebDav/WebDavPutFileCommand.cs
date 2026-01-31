// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Jobs;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV PUT operation (upload file)
/// </summary>
public record WebDavPutFileCommand(
    Guid UserId,
    string Path,
    Stream Content,
    string? ContentType,
    bool Overwrite = true) : IRequest<WebDavPutFileResult>;

/// <summary>
/// Result of WebDAV PUT operation
/// </summary>
public record WebDavPutFileResult(
    bool Success,
    bool Created,
    WebDavPutFileError? Error = null);

public enum WebDavPutFileError
{
    ParentNotFound,
    IsCollection,
    InvalidName,
    Conflict
}

/// <summary>
/// Handler for WebDAV PUT operation
/// </summary>
public class WebDavPutFileCommandHandler(
    CottonDbContext _dbContext,
    ILayoutService _layouts,
    IStoragePipeline _storage,
    IWebDavPathResolver _pathResolver,
    FileManifestService _fileManifestService,
    ILogger<WebDavPutFileCommandHandler> _logger)
    : IRequestHandler<WebDavPutFileCommand, WebDavPutFileResult>
{
    public async Task<WebDavPutFileResult> Handle(WebDavPutFileCommand request, CancellationToken ct)
    {
        // Check if path points to existing collection
        var existing = await _pathResolver.ResolvePathAsync(request.UserId, request.Path, ct);
        if (existing.Found && existing.IsCollection)
        {
            _logger.LogDebug("WebDAV PUT: Path is a collection: {Path}", request.Path);
            return new WebDavPutFileResult(false, false, WebDavPutFileError.IsCollection);
        }

        // Get parent node
        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            _logger.LogDebug("WebDAV PUT: Parent not found for path: {Path}", request.Path);
            return new WebDavPutFileResult(false, false, WebDavPutFileError.ParentNotFound);
        }

        // Validate name
        if (!NameValidator.TryNormalizeAndValidate(parentResult.ResourceName, out _, out var errorMessage))
        {
            _logger.LogDebug("WebDAV PUT: Invalid name: {Name}, Error: {Error}", parentResult.ResourceName, errorMessage);
            return new WebDavPutFileResult(false, false, WebDavPutFileError.InvalidName);
        }

        var nameKey = NameValidator.NormalizeAndGetNameKey(parentResult.ResourceName);
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);

        // Check for conflict with folder
        var folderExists = await _dbContext.Nodes
            .AnyAsync(n => n.ParentId == parentResult.ParentNode.Id
                && n.OwnerId == request.UserId
                && n.NameKey == nameKey
                && n.LayoutId == layout.Id
                && n.Type == NodeType.Default, ct);

        if (folderExists)
        {
            _logger.LogDebug("WebDAV PUT: Conflict with existing folder: {Path}", request.Path);
            return new WebDavPutFileResult(false, false, WebDavPutFileError.Conflict);
        }

        bool created = !existing.Found;

        // Read content and create chunk
        using var memoryStream = new MemoryStream();
        await request.Content.CopyToAsync(memoryStream, ct);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var contentHash = Hasher.HashData(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var storageKey = Hasher.ToHexStringHash(contentHash);

        // Check if chunk is being garbage collected
        if (GarbageCollectorJob.IsChunkBeingDeleted(storageKey))
        {
            _logger.LogWarning("WebDAV PUT: Chunk is being garbage collected, retrying later: {Hash}", storageKey);
            // Wait a bit and retry - GC should be quick
            await Task.Delay(100, ct);
        }

        // Find or create chunk
        var chunk = await _layouts.FindChunkAsync(contentHash);
        if (chunk is null)
        {
            await _storage.WriteAsync(storageKey, memoryStream, new PipelineContext());
            chunk = new Chunk
            {
                Hash = contentHash,
                SizeBytes = memoryStream.Length,
                CompressionAlgorithm = CompressionProcessor.Algorithm
            };
            await _dbContext.Chunks.AddAsync(chunk, ct);
        }
        else if (chunk.GCScheduledAfter.HasValue)
        {
            chunk.GCScheduledAfter = null;
            _dbContext.Chunks.Update(chunk);
        }

        // Create chunk ownership
        var ownershipExists = await _dbContext.ChunkOwnerships
            .AnyAsync(co => co.ChunkHash == contentHash && co.OwnerId == request.UserId, ct);

        if (!ownershipExists)
        {
            var ownership = new ChunkOwnership
            {
                ChunkHash = contentHash,
                OwnerId = request.UserId
            };
            await _dbContext.ChunkOwnerships.AddAsync(ownership, ct);
        }

        // Determine content type
        var contentType = request.ContentType ?? "application/octet-stream";

        // Create file manifest
        var fileManifest = await _dbContext.FileManifests
            .FirstOrDefaultAsync(f => f.ProposedContentHash == contentHash || f.ComputedContentHash == contentHash, ct);

        fileManifest ??= await _fileManifestService.CreateNewFileManifestAsync(
            [chunk],
            parentResult.ResourceName,
            contentType,
            contentHash,
            ct);

        // Update or create node file
        if (existing.Found && existing.NodeFile is not null)
        {
            // Update existing file
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == existing.NodeFile.Id, ct);

            nodeFile.FileManifestId = fileManifest.Id;
            _dbContext.NodeFiles.Update(nodeFile);
        }
        else
        {
            // Create new file
            var nodeFile = new NodeFile
            {
                OwnerId = request.UserId,
                NodeId = parentResult.ParentNode.Id,
                FileManifestId = fileManifest.Id,
            };
            nodeFile.SetName(parentResult.ResourceName);

            await _dbContext.NodeFiles.AddAsync(nodeFile, ct);
            await _dbContext.SaveChangesAsync(ct);

            nodeFile.OriginalNodeFileId = nodeFile.Id;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("WebDAV PUT: {Action} file {Path} for user {UserId}",
            created ? "Created" : "Updated", request.Path, request.UserId);

        return new WebDavPutFileResult(true, created);
    }
}
