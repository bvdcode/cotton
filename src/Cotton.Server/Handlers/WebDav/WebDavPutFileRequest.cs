// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
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
public record WebDavPutFileRequest(
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
    WebDavPutFileError? Error = null,
    Guid? NodeFileId = null);

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
public class WebDavPutFileRequestHandler(
    ILayoutService _layouts,
    CottonDbContext _dbContext,
    SettingsProvider _settings,
    ISchedulerFactory _scheduler,
    NodeFileHistoryService _history,
    IChunkIngestService _chunkIngest,
    IWebDavPathResolver _pathResolver,
    FileManifestService _fileManifestService,
    IEventNotificationService _eventNotification,
    ILogger<WebDavPutFileRequestHandler> _logger)
    : IRequestHandler<WebDavPutFileRequest, WebDavPutFileResult>
{
    private sealed record PutTarget(
        WebDavResolveResult Existing,
        WebDavParentResult Parent,
        string ResourceName,
        string NameKey,
        bool Created);

    private sealed record PutContent(List<Chunk> Chunks, byte[] FileHash, long TotalBytes);

    public async Task<WebDavPutFileResult> Handle(WebDavPutFileRequest request, CancellationToken ct)
    {
        var (target, targetError) = await TryResolveAndValidateTargetAsync(request, ct);
        if (targetError != null)
        {
            return targetError;
        }

        var (content, contentError) = await TryReadAndValidateContentAsync(request, ct);
        if (contentError != null)
        {
            return contentError;
        }

        string contentType = ResolveContentType(request.ContentType, target!.ResourceName);
        var fileManifest = await GetOrCreateFileManifestAsync(
            chunks: content!.Chunks,
            fileHash: content.FileHash,
            resourceName: target.ResourceName,
            contentType: contentType,
            ct);

        await UpsertNodeFileAsync(request, target, fileManifest.Id, ct);
        await _dbContext.SaveChangesAsync(ct);

        var resultNodeFile = await LoadResultNodeFileAsync(request, target, ct);
        await NotifyPutCompletedAsync(request, created: target.Created, chunkCount: content.Chunks.Count, nodeFileId: resultNodeFile.Id, ct);
        return new WebDavPutFileResult(true, target.Created, null, resultNodeFile.Id);
    }

    private async Task<(PutTarget? Target, WebDavPutFileResult? Error)> TryResolveAndValidateTargetAsync(WebDavPutFileRequest request, CancellationToken ct)
    {
        var existing = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (existing.Found && existing.IsCollection)
        {
            return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.IsCollection));
        }

        var parentResult = await _pathResolver.GetParentNodeAsync(request.UserId, request.Path, ct);
        if (!parentResult.Found || parentResult.ParentNode is null || parentResult.ResourceName is null)
        {
            return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.ParentNotFound));
        }

        if (!NameValidator.TryNormalizeAndValidate(parentResult.ResourceName, out _, out _))
        {
            return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.InvalidName));
        }

        var nameKey = NameValidator.NormalizeAndGetNameKey(parentResult.ResourceName);
        var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(request.UserId);

        var folderExists = await _dbContext.Nodes
            .AnyAsync(n => n.ParentId == parentResult.ParentNode.Id
                && n.OwnerId == request.UserId
                && n.NameKey == nameKey
                && n.LayoutId == layout.Id
                && n.Type == WebDavPathResolver.DefaultNodeType, ct);

        if (folderExists)
        {
            return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.Conflict));
        }

        if (existing.Found && existing.NodeFile is not null && !request.Overwrite)
        {
            return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.PreconditionFailed));
        }

        bool created = !existing.Found;
        return (new PutTarget(existing, parentResult, parentResult.ResourceName, nameKey, created), null);
    }

    private async Task<(PutContent? Content, WebDavPutFileResult? Error)> TryReadAndValidateContentAsync(WebDavPutFileRequest request, CancellationToken ct)
    {
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
                    request.ContentLength.Value,
                    totalBytes,
                    request.Path,
                    request.UserId);

                return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.UploadAborted));
            }
        }

        if (totalBytes == 0)
        {
            if (request.ContentLength.HasValue && request.ContentLength.Value == 0)
            {
                chunks = await CreateEmptyChunkAsync(request.UserId, ct);
                fileHash = Hasher.HashData([]);
                totalBytes = 0;
            }
            else
            {
                _logger.LogWarning(
                    "WebDAV PUT got 0 bytes but Content-Length was {CL}. Treating as aborted. Path: {Path}, User: {UserId}",
                    request.ContentLength,
                    request.Path,
                    request.UserId);

                return (null, new WebDavPutFileResult(false, false, WebDavPutFileError.UploadAborted));
            }
        }

        return (new PutContent(chunks, fileHash, totalBytes), null);
    }

    private static string ResolveContentType(string? contentType, string resourceName)
    {
        FileExtensionContentTypeProvider contentTypeProvider = new();
        return contentType ??
            (contentTypeProvider.TryGetContentType(resourceName, out var detectedType)
                ? detectedType
                : "application/octet-stream");
    }

    private async Task<FileManifest> GetOrCreateFileManifestAsync(
        List<Chunk> chunks,
        byte[] fileHash,
        string resourceName,
        string contentType,
        CancellationToken ct)
    {
        var fileManifest = await _dbContext.FileManifests
            .FirstOrDefaultAsync(f => f.ProposedContentHash == fileHash || f.ComputedContentHash == fileHash, ct);

        fileManifest ??= await _fileManifestService.CreateNewFileManifestAsync(
            chunks,
            resourceName,
            contentType,
            fileHash,
            ct);

        return fileManifest;
    }

    private async Task UpsertNodeFileAsync(WebDavPutFileRequest request, PutTarget target, Guid fileManifestId, CancellationToken ct)
    {
        if (target.Existing.Found && target.Existing.NodeFile is not null)
        {
            var nodeFile = await _dbContext.NodeFiles
                .FirstAsync(f => f.Id == target.Existing.NodeFile.Id, ct);

            var previousManifest = await _dbContext.FileManifests
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == nodeFile.FileManifestId, ct);

            if (previousManifest?.SizeBytes == 0)
            {
                nodeFile.FileManifestId = fileManifestId;
            }
            else
            {
                await _history.SaveVersionAndUpdateManifestAsync(nodeFile, fileManifestId, request.UserId, ct);
            }

            return;
        }

        var createdNodeFile = new NodeFile
        {
            OwnerId = request.UserId,
            NodeId = target.Parent.ParentNode!.Id,
            FileManifestId = fileManifestId,
        };
        createdNodeFile.SetName(target.ResourceName);

        await _dbContext.NodeFiles.AddAsync(createdNodeFile, ct);
        await _dbContext.SaveChangesAsync(ct);

        createdNodeFile.OriginalNodeFileId = createdNodeFile.Id;
    }

    private async Task<NodeFile> LoadResultNodeFileAsync(WebDavPutFileRequest request, PutTarget target, CancellationToken ct)
    {
        if (target.Existing.Found && target.Existing.NodeFile is not null)
        {
            return await _dbContext.NodeFiles.FirstAsync(f => f.Id == target.Existing.NodeFile.Id, ct);
        }

        return await _dbContext.NodeFiles
            .Where(f => f.NodeId == target.Parent.ParentNode!.Id
                && f.OwnerId == request.UserId
                && f.NameKey == target.NameKey)
            .OrderByDescending(f => f.CreatedAt)
            .FirstAsync(ct);
    }

    private async Task NotifyPutCompletedAsync(
        WebDavPutFileRequest request,
        bool created,
        int chunkCount,
        Guid nodeFileId,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "WebDAV PUT: {Action} file {Path} ({ChunkCount} chunks) for user {UserId}",
            created ? "Created" : "Updated",
            request.Path,
            chunkCount,
            request.UserId);

        await _scheduler.TriggerJobAsync<GeneratePreviewJob>();

        if (created)
        {
            await _eventNotification.NotifyFileCreatedAsync(nodeFileId, ct);
        }
        else
        {
            await _eventNotification.NotifyFileUpdatedAsync(nodeFileId, ct);
        }
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
