// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Extensions;
using Cotton.Server.Jobs;
using Cotton.Server.Models;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Quartz;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
        ISchedulerFactory _scheduler,
        ILogger<FileController> _logger,
        StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpDelete($"{Routes.Files}/{{nodeFileId:guid}}")]
        public async Task<IActionResult> DeleteFile([FromRoute] Guid nodeFileId)
        {
            Guid userId = User.GetUserId();
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.Node)
                .FirstOrDefaultAsync(x => x.Id == nodeFileId && x.OwnerId == userId)
                ?? throw new EntityNotFoundException(nameof(FileManifest));
            if (nodeFile.Node.Type == NodeType.Trash)
            {
                return this.ApiBadRequest("File is already deleted from the layout.");
            }
            var trashNode = await _layouts.GetUserTrashNodeAsync(userId);
            nodeFile.NodeId = trashNode.Id;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("User {UserId} deleted file {NodeFileId} to trash.", userId, nodeFileId);
            return NoContent();
        }

        [Authorize]
        [HttpGet($"{Routes.Files}/{{nodeFileId:guid}}/download-link")]
        public async Task<IActionResult> DownloadFile([FromRoute] Guid nodeFileId, [FromQuery] int expireAfterMinutes = 15)
        {
            const int maxExpireMinutes = 60 * 24 * 365; // 1 year
            ArgumentOutOfRangeException.ThrowIfGreaterThan(expireAfterMinutes, maxExpireMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expireAfterMinutes, nameof(expireAfterMinutes));

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
                CreatedByUserId = User.GetUserId(),
                FileManifestId = nodeFile.FileManifestId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expireAfterMinutes),
                Token = StringHelpers.CreateRandomString(128),
            };
            await _dbContext.DownloadTokens.AddAsync(newToken);
            await _dbContext.SaveChangesAsync();
            string link = Routes.Files + $"/{nodeFileId}/download?token={newToken.Token}";
            return Ok(link);
        }

        [HttpGet($"{Routes.Files}/{{nodeFileId:guid}}/download")]
        public async Task<IActionResult> DownloadFileByToken(
            [FromRoute] Guid nodeFileId,
            [FromQuery] string token)
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
                ChunkLengths = nodeFile.FileManifest.FileManifestChunks.ToDictionary(x => Hasher.ToHexStringHash(x.ChunkHash), x => x.Chunk.SizeBytes),
            };
            Stream stream = _storage.GetBlobStream(uids, context);
            Response.Headers.CacheControl = "private, no-store";
            var entityTag = EntityTagHeaderValue.Parse($"\"sha256-{Hasher.ToHexStringHash(nodeFile.FileManifest.ProposedContentHash)}\"");
            return File(
                stream,
                nodeFile.FileManifest.ContentType,
                nodeFile.Name,
                lastModified: new(nodeFile.CreatedAt),
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
            bool nameExists = await _dbContext.NodeFiles
                .AnyAsync(x => x.NodeId == node.Id && x.OwnerId == userId && x.NameKey == nameKey);
            if (nameExists)
            {
                return this.ApiConflict("A file with the same name key already exists in the target node: " + nameKey);
            }

            List<Chunk> chunks = await GetChunksAsync(request.ChunkHashes);

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalizedName, out string errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest($"Invalid file name: {errorMessage}");
            }

            // Normalize chunk hashes to lowercase for storage access
            string[] normalizedChunkHashes = [.. request.ChunkHashes
                .Select(Hasher.FromHexStringHash)
                .Select(Hasher.ToHexStringHash)];

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
            List<Chunk> chunks = [];
            foreach (var item in chunkHashes)
            {
                byte[] hashBytes = Hasher.FromHexStringHash(item);
                var foundChunk = await _layouts.FindChunkAsync(hashBytes) ?? throw new EntityNotFoundException(nameof(Chunk));
                // TODO: Add safety check to ensure chunks belong to the user
                // Must depend on owner/user authentication, no reason to delay for the same user
                chunks.Add(foundChunk);
            }
            return chunks;
        }

        private async Task<FileManifest> CreateNewFileManifestAsync(List<Chunk> chunks, CreateFileRequest request, byte[] proposedContentHash)
        {
            var newFileManifest = new FileManifest()
            {
                ContentType = request.ContentType,
                SizeBytes = chunks.Sum(x => x.SizeBytes),
                ProposedContentHash = proposedContentHash,
            };
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
