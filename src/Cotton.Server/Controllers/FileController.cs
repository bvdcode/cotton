// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Database.Models.Enums;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Extensions;
using Cotton.Topology;
using Cotton.Validators;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IStoragePipeline _storage,
        CottonDbContext _dbContext,
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

        // TODO: Authorization: Ensure the user has access to this file
        [HttpGet($"{Routes.Files}/{{nodeFileId:guid}}/download")]
        public async Task<IActionResult> DownloadFile([FromRoute] Guid nodeFileId)
        {
            var nodeFile = await _dbContext.NodeFiles
                .Include(x => x.FileManifest)
                .ThenInclude(x => x.FileManifestChunks)
                .SingleOrDefaultAsync(x => x.Id == nodeFileId);
            if (nodeFile == null)
            {
                return CottonResult.NotFound("Node file not found");
            }
            string[] hashes = [.. nodeFile.FileManifest.FileManifestChunks
                .OrderBy(x => x.ChunkOrder)
                .Select(x => Hasher.ToHexStringHash(x.ChunkHash).ToLowerInvariant())];
            Stream stream = _storage.GetBlobStream(hashes);
            _logger.LogInformation("File {NodeFileId} downloaded.", nodeFileId);
            return File(stream, nodeFile.FileManifest.ContentType, nodeFile.Name);
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

            // TODO: Get rid of this (or not?)
            using var blob = _storage.GetBlobStream(normalizedChunkHashes);
            byte[] computedHash = await Hasher.HashDataAsync(blob);
            if (!string.IsNullOrWhiteSpace(request.Hash))
            {
                byte[] providedHash = Hasher.FromHexStringHash(request.Hash);
                if (!computedHash.SequenceEqual(providedHash))
                {
                    return CottonResult.BadRequest("Provided Hash does not match the computed hash of the file.");
                }
            }

            var newFile = await _dbContext.FileManifests.FindAsync(computedHash)
                ?? await CreateNewFileManifestAsync(chunks, request, computedHash);

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

        private async Task<FileManifest> CreateNewFileManifestAsync(List<Chunk> chunks, CreateFileRequest request, byte[] computedHash)
        {
            var newFile = new FileManifest()
            {
                ContentType = request.ContentType,
                SizeBytes = chunks.Sum(x => x.SizeBytes),
                Hash = computedHash,
            };
            await _dbContext.FileManifests.AddAsync(newFile);

            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    ChunkHash = chunks[i].Hash,
                    FileManifestHash = computedHash,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk);
            }
            await _dbContext.SaveChangesAsync();
            return newFile;
        }
    }
}
