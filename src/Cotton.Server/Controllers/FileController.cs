// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Files;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Storage.Pipelines;
using Cotton.Topology;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.Helpers;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Quartz;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IMediator _mediator,
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ISchedulerFactory _scheduler,
        ILogger<FileController> _logger,
        StorageLayoutService _layouts) : ControllerBase
    {
        private const int DefaultSharedFileTokenLength = 16;
        private static readonly FileExtensionContentTypeProvider fileExtensionContentTypeProvider = new();

        [Authorize]
        [HttpDelete($"{Routes.Files}/{{nodeFileId:guid}}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] bool skipTrash = false)
        {
            Guid userId = User.GetUserId();
            DeleteFileQuery query = new(userId, nodeFileId, skipTrash);
            await _mediator.Send(query);
            return NoContent();
        }

        [Authorize]
        [HttpPatch($"{Routes.Files}/{{nodeFileId:guid}}/rename")]
        public async Task<IActionResult> RenameFile(
            [FromRoute] Guid nodeFileId,
            [FromBody] RenameFileRequest request)
        {
            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name,
                out string normalizedName,
                out string? errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest(errorMessage);
            }

            Guid userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null)
            {
                return CottonResult.NotFound("File not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            // Check for duplicate files in the same folder
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x =>
                    x.NodeId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.Id != nodeFileId);
            if (fileExists)
            {
                return this.ApiConflict("A file with the same name key already exists in this folder: " + nameKey);
            }

            // Check for duplicate nodes (subfolders) in the same folder
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == nodeFile.NodeId &&
                    x.OwnerId == userId &&
                    x.Type == nodeFile.Node.Type &&
                    x.NameKey == nameKey);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in this folder: " + nameKey);
            }

            nodeFile.SetName(request.Name);
            await _dbContext.SaveChangesAsync();

            var mapped = nodeFile.Adapt<FileManifestDto>();
            return Ok(mapped);
        }

        [Authorize]
        [HttpGet($"{Routes.Files}/{{nodeFileId:guid}}/download-link")]
        public async Task<IActionResult> DownloadFile(
            [FromRoute] Guid nodeFileId,
            [FromQuery] int expireAfterMinutes = 1440,
            [FromQuery] string? customToken = "",
            [FromQuery] bool deleteAfterUse = false)
        {
            const int maxExpireMinutes = 60 * 24 * 365; // 1 year
            ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

            if (!string.IsNullOrWhiteSpace(customToken))
            {
                bool exists = await _dbContext.DownloadTokens
                    .AnyAsync(x => x.Token == customToken);
                if (exists)
                {
                    return this.ApiConflict("The custom token is already in use. Please choose a different one.");
                }
            }

            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null)
            {
                return CottonResult.NotFound("Node file not found");
            }

            DownloadToken newToken = new()
            {
                DeleteAfterUse = deleteAfterUse,
                CreatedByUserId = User.GetUserId(),
                FileManifestId = nodeFile.FileManifestId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expireAfterMinutes),
                Token = StringHelpers.CreateRandomString(DefaultSharedFileTokenLength),
            };
            await _dbContext.DownloadTokens.AddAsync(newToken);
            await _dbContext.SaveChangesAsync();
            string link = Routes.Files + $"/{nodeFileId}/download?token={newToken.Token}";
            return Ok(link);
        }

        [Authorize]
        [HttpPatch($"{Routes.Files}/{{nodeFileId:guid}}/update-content")]
        public async Task<IActionResult> UpdateFileContent(
            [FromRoute] Guid nodeFileId,
            [FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .Where(x => x.Id == nodeFileId && x.OwnerId == userId)
                .SingleOrDefaultAsync();
            if (nodeFile == null)
            {
                return this.ApiNotFound("Node file not found.");
            }
            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            if (nodeFile.FileManifest.ProposedContentHash.SequenceEqual(proposedHash))
            {
                return Ok();
            }
            List<Chunk> chunks = await GetChunksAsync(request.ChunkHashes);
            var newFile = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash)
                ?? await CreateNewFileManifestAsync(chunks, request, proposedHash);
            nodeFile.FileManifestId = newFile.Id;
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            return Ok();
        }

        [HttpGet($"{Routes.Files}/{{nodeFileId:guid}}/download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token,
            [FromQuery] bool download = true)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return CottonResult.NotFound("File not found");
            }
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .ThenInclude(x => x.Chunk)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null)
            {
                return CottonResult.NotFound("File not found");
            }
            var downloadToken = await _dbContext.DownloadTokens
                .FirstOrDefaultAsync(x => x.Token == token && x.FileManifestId == nodeFile.FileManifestId);
            if (downloadToken == null || (downloadToken.ExpiresAt.HasValue && downloadToken.ExpiresAt.Value < DateTime.UtcNow))
            {
                return CottonResult.NotFound("File not found");
            }

            string[] uids = nodeFile.FileManifest.FileManifestChunks.GetChunkHashes();
            PipelineContext context = new()
            {
                FileSizeBytes = nodeFile.FileManifest.SizeBytes,
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.GetChunkLengths()
            };
            Stream stream = _storage.GetBlobStream(uids, context);
            Response.Headers.ContentEncoding = "identity";
            Response.Headers.CacheControl = "private, no-store, no-transform";
            var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash)}\"");

            if (downloadToken.DeleteAfterUse)
            {
                Response.OnCompleted(async () =>
                {
                    _dbContext.DownloadTokens.Remove(downloadToken);
                    await _dbContext.SaveChangesAsync();
                });
            }

            var lastModified = new DateTimeOffset(nodeFile.CreatedAt);
            return File(
                stream,
                nodeFile.FileManifest.ContentType,
                fileDownloadName: download ? nodeFile.Name : null,
                lastModified: lastModified,
                entityTag: entityTag,
                enableRangeProcessing: true);
        }

        [Authorize]
        [HttpPost(Routes.Files + "/from-chunks")]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            var layout = await _layouts.GetOrCreateLatestUserLayoutAsync(userId);
            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.Type == NodeType.Default && x.OwnerId == userId && x.LayoutId == layout.Id)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return this.ApiNotFound("Layout node not found.");
            }

            string nameKey = NameValidator.NormalizeAndGetNameKey(request.Name);

            // Check for duplicate files in the target folder
            bool fileExists = await _dbContext.NodeFiles
                .AnyAsync(x => x.NodeId == node.Id && x.OwnerId == userId && x.NameKey == nameKey);
            if (fileExists)
            {
                return this.ApiConflict("A file with the same name key already exists in the target node: " + nameKey);
            }

            // Check for duplicate nodes (subfolders) in the target folder
            bool nodeExists = await _dbContext.Nodes
                .AnyAsync(x =>
                    x.ParentId == node.Id &&
                    x.OwnerId == userId &&
                    x.NameKey == nameKey &&
                    x.Type == NodeType.Default);
            if (nodeExists)
            {
                return this.ApiConflict("A folder with the same name key already exists in the target node: " + nameKey);
            }

            List<Chunk> chunks = await GetChunksAsync(request.ChunkHashes);

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalizedName, out string errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest($"Invalid file name: {errorMessage}");
            }

            byte[] proposedHash = Hasher.FromHexStringHash(request.Hash);
            var newFile = await _dbContext.FileManifests
                .FirstOrDefaultAsync(x => x.ComputedContentHash == proposedHash || x.ProposedContentHash == proposedHash)
                ?? await CreateNewFileManifestAsync(chunks, request, proposedHash);

            NodeFile newNodeFile = new()
            {
                Node = node,
                OwnerId = userId,
                FileManifest = newFile,
            };
            newNodeFile.SetName(request.Name);
            if (request.Validate && newFile.ComputedContentHash == null)
            {
                string[] hashes = newFile.FileManifestChunks.GetChunkHashes();
                PipelineContext pipelineContext = new()
                {
                    FileSizeBytes = newFile.SizeBytes
                };
                using Stream stream = _storage.GetBlobStream(hashes, pipelineContext);
                var computedContentHash = Hasher.HashData(stream);
                if (!computedContentHash.SequenceEqual(proposedHash))
                {
                    _logger.LogWarning("File content hash mismatch for user {UserId}, file {FileName}. Expected {ExpectedHash}, computed {ComputedHash}.",
                        userId,
                        request.Name,
                        request.Hash,
                        Hasher.ToHexStringHash(computedContentHash));
                    return this.ApiBadRequest("File content hash does not match the provided hash.");
                }
                newFile.ComputedContentHash = computedContentHash;
            }
            await _dbContext.NodeFiles.AddAsync(newNodeFile);
            if (!request.OriginalNodeFileId.HasValue)
            {
                await _dbContext.SaveChangesAsync();
                newNodeFile.OriginalNodeFileId = newNodeFile.Id;
            }
            else
            {
                newNodeFile.OriginalNodeFileId = request.OriginalNodeFileId.Value;
            }
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<ComputeManifestHashesJob>();
            await _scheduler.TriggerJobAsync<GeneratePreviewJob>();
            return Ok();
        }

        private async Task<List<Chunk>> GetChunksAsync(string[] chunkHashes)
        {
            Guid userId = User.GetUserId();

            List<byte[]> normalizedHashes = [.. chunkHashes.Select(Hasher.FromHexStringHash)];
            List<Chunk> ownedChunks = await _dbContext.Chunks
                .Where(c => normalizedHashes.Contains(c.Hash))
                .Where(c => _dbContext.ChunkOwnerships.Any(co => co.ChunkHash == c.Hash && co.OwnerId == userId))
                .ToListAsync();

            var chunkMap = ownedChunks.ToDictionary(c => Hasher.ToHexStringHash(c.Hash), StringComparer.OrdinalIgnoreCase);
            List<Chunk> result = [];
            foreach (var hash in chunkHashes)
            {
                if (!chunkMap.TryGetValue(hash, out var chunk))
                {
                    throw new EntityNotFoundException(nameof(Chunk));
                }
                result.Add(chunk);
            }
            return result;
        }

        private async Task<FileManifest> CreateNewFileManifestAsync(
            List<Chunk> chunks,
            CreateFileRequest request,
            byte[] proposedContentHash)
        {
            var newFileManifest = new FileManifest()
            {
                ContentType = request.ContentType,
                SizeBytes = chunks.Sum(x => x.SizeBytes),
                ProposedContentHash = proposedContentHash,
            };
            if (newFileManifest.ContentType == "application/octet-stream")
            {
                string? extension = Path.GetExtension(request.Name);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    bool recognized = fileExtensionContentTypeProvider.TryGetContentType(request.Name, out string? contentType);
                    if (recognized && !string.IsNullOrWhiteSpace(contentType))
                    {
                        newFileManifest.ContentType = contentType;
                    }
                }
            }

            await _dbContext.FileManifests.AddAsync(newFileManifest);
            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    ChunkHash = chunks[i].Hash,
                    FileManifest = newFileManifest,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk);
            }
            await _dbContext.SaveChangesAsync();
            return newFileManifest;
        }
    }
}
