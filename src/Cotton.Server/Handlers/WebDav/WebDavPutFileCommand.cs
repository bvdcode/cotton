// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Jobs;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.WebDav;
using Cotton.Topology.Abstractions;
using Cotton.Validators;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.WebDav;

/// <summary>
/// Command for WebDAV PUT operation (upload file)
/// </summary>
public record WebDavPutFileCommand(
    Guid UserId,
    string Path,
    Stream Content,
    string? ContentType,
    bool Overwrite = true,
    long? ContentLength = null) : IRequest<WebDavPutFileResult>;

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
    Conflict,
    PreconditionFailed,
    UploadAborted
}

/// <summary>
/// Handler for WebDAV PUT operation with streaming chunk processing.
/// Processes large files without loading them entirely into memory.
/// </summary>
public class WebDavPutFileCommandHandler(
    ILayoutService _layouts,
    CottonDbContext _dbContext,
    SettingsProvider _settings,
    ISchedulerFactory _scheduler,
    NodeFileHistoryService _history,
    IChunkIngestService _chunkIngest,
    IWebDavPathResolver _pathResolver,
    FileManifestService _fileManifestService,
    ILogger<WebDavPutFileCommandHandler> _logger)
    : IRequestHandler<WebDavPutFileCommand, WebDavPutFileResult>
{
    public async Task<WebDavPutFileResult> Handle(WebDavPutFileCommand request, CancellationToken ct)
    {
        // Check if path points to existing collection
        var existing = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (existing.Found && existing.IsCollection)
        {
            return new WebDavPutFileResult(false, false, WebDavPutFileError.IsCollection);
        }

        // Get parent node
        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            return new WebDavPutFileResult(false, false, WebDavPutFileError.ParentNotFound);
        }

        // Validate name
        if (!NameValidator.TryNormalizeAndValidate(parentResult.ResourceName, out _, out var errorMessage))
        {
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
                && n.Type == WebDavPathResolver.DefaultNodeType, ct);

        if (folderExists)
        {
            return new WebDavPutFileResult(false, false, WebDavPutFileError.Conflict);
        }

        // If file exists and overwrite is false then fail per WebDAV semantics.
        if (existing.Found && existing.NodeFile is not null && !request.Overwrite)
        {
            return new WebDavPutFileResult(false, false, WebDavPutFileError.PreconditionFailed);
        }

        bool created = !existing.Found;
        // Process stream in chunks without loading entire file into memory
        var (chunks, fileHash) = await ProcessStreamInChunksAndHashAsync(request.Content, request.UserId, ct);
        long totalBytes = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            totalBytes += chunks[i].SizeBytes;
        }

        if (request.ContentLength.HasValue && request.ContentLength.Value > 0)
        {
            if (totalBytes == 0 || totalBytes != request.ContentLength.Value)
            {
                _logger.LogWarning(
                    "WebDAV PUT aborted/truncated: expected length {Expected}, got {Actual} bytes. Path: {Path}, User: {UserId}",
                    request.ContentLength.Value, totalBytes, request.Path, request.UserId);

                return new WebDavPutFileResult(false, false, WebDavPutFileError.UploadAborted);
            }
        }

        if (totalBytes == 0)
        {
            // Allow empty file only when client explicitly indicates Content-Length: 0.
            // Otherwise this is likely an aborted upload (e.g. client closed connection early).
            if (request.ContentLength.HasValue && request.ContentLength.Value == 0)
            {
                chunks = await CreateEmptyChunkAsync(request.UserId, ct);
                fileHash = Hasher.HashData([]);
            }
            else
            {
                _logger.LogWarning(
                    "WebDAV PUT got 0 bytes but Content-Length was {CL}. Treating as aborted. Path: {Path}, User: {UserId}",
                    request.ContentLength, request.Path, request.UserId);

                return new WebDavPutFileResult(false, false, WebDavPutFileError.UploadAborted);
            }
        }

        // Determine content type
        FileExtensionContentTypeProvider contentTypeProvider = new();
        var contentType = request.ContentType ??
            (contentTypeProvider.TryGetContentType(parentResult.ResourceName, out var detectedType)
                ? detectedType
                : "application/octet-stream");

        // Find or create file manifest
        var fileManifest = await _dbContext.FileManifests
            .FirstOrDefaultAsync(f => f.ProposedContentHash == fileHash || f.ComputedContentHash == fileHash, ct);

        fileManifest ??= await _fileManifestService.CreateNewFileManifestAsync(
            chunks,
            parentResult.ResourceName,
            contentType,
            fileHash,
            ct);

        // Update or create node file
        if (existing.Found && existing.NodeFile is not null)
        {
            // Update existing file
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == existing.NodeFile.Id, ct);

            // If previous version is an empty file, don't create a version entry in trash.
            // Treat it as a simple overwrite by replacing the manifest.
            var previousManifest = await _dbContext.FileManifests
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == nodeFile.FileManifestId, ct);

            if (previousManifest?.SizeBytes == 0)
            {
                nodeFile.FileManifestId = fileManifest.Id;
            }
            else
            {
                await _history.SaveVersionAndUpdateManifestAsync(nodeFile, fileManifest.Id, request.UserId, ct);
            }
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

        _logger.LogInformation("WebDAV PUT: {Action} file {Path} ({ChunkCount} chunks) for user {UserId}",
            created ? "Created" : "Updated", request.Path, chunks.Count, request.UserId);

        await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
        return new WebDavPutFileResult(true, created);
    }

    /// <summary>
    /// Processes the input stream in chunks, creating and storing each chunk on the fly.
    /// Uses a rented buffer to avoid allocations.
    /// </summary>
    private async Task<(List<Chunk> Chunks, byte[] FileHash)> ProcessStreamInChunksAndHashAsync(
        Stream inputStream,
        Guid userId,
        CancellationToken ct)
    {
        var settings = _settings.GetServerSettings();
        int chunkSize = settings.MaxChunkSizeBytes;
        var chunks = new List<Chunk>();

        using var fileHasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);

        // Rent a buffer from the array pool to avoid allocations
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(chunkSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await ReadExactlyAsync(inputStream, buffer, chunkSize, ct)) > 0)
            {
                fileHasher.AppendData(buffer, 0, bytesRead);
                var chunk = await ProcessSingleChunkAsync(buffer, bytesRead, userId, ct);
                chunks.Add(chunk);
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        return (chunks, fileHasher.GetHashAndReset());
    }

    /// <summary>
    /// Reads exactly the specified number of bytes or until end of stream.
    /// </summary>
    private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }

    /// <summary>
    /// Processes a single chunk: computes hash, stores if new, creates ownership.
    /// </summary>
    private async Task<Chunk> ProcessSingleChunkAsync(byte[] buffer, int length, Guid userId, CancellationToken ct)
    {
        return await _chunkIngest.UpsertChunkAsync(userId, buffer, length, ct);
    }

    /// <summary>
    /// Creates an empty chunk for empty files.
    /// </summary>
    private async Task<List<Chunk>> CreateEmptyChunkAsync(Guid userId, CancellationToken ct)
    {
        var chunk = await _chunkIngest.UpsertChunkAsync(userId, [], 0, ct);
        return [chunk];
    }
}
