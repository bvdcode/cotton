// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Models.Dto;
using Cotton.Server.Validators;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Cotton.Server.Database.Models;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Cotton.Server.Database.Models.Enums;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext, IStorage _storage, StorageLayoutService _layouts) : ControllerBase
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
                return CottonResult.BadRequest("File is already deleted from the layout.");
            }
            var trashNode = await _layouts.GetUserTrashNodeAsync(userId);
            nodeFile.NodeId = trashNode.Id;
            await _dbContext.SaveChangesAsync();
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
                .Select(x => Convert.ToHexString(x.ChunkSha256))];
            Stream stream = _storage.GetBlobStream(hashes);
            return File(stream, nodeFile.FileManifest.ContentType, nodeFile.Name);
        }

        [Authorize]
        [HttpPost(Routes.Files)]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            var node = await _dbContext.Nodes
                .Where(x => x.Id == request.NodeId && x.Type == NodeType.Default && x.OwnerId == userId)
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

            List<Chunk> chunks = [];
            foreach (var item in request.ChunkHashes)
            {
                var foundChunk = await _layouts.FindChunkAsync(item);
                if (foundChunk == null)
                {
                    return CottonResult.BadRequest($"Chunk with hash {item} not found.");
                }
                // TODO: Add safety check to ensure chunks belong to the user
                // Must depend on owner/user authentication, no reason to delay for the same user
                chunks.Add(foundChunk);
            }

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalizedName, out string errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest($"Invalid file name: {errorMessage}");
            }

            byte[] clientComputedHash = Convert.FromHexString(request.Sha256);
            var newFile = await _dbContext.FileManifests.FindAsync(clientComputedHash);
            if (newFile == null)
            {
                newFile = new()
                {
                    ContentType = request.ContentType,
                    SizeBytes = chunks.Sum(x => x.SizeBytes),
                    Sha256 = clientComputedHash,
                };
                await _dbContext.FileManifests.AddAsync(newFile);
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    ChunkSha256 = chunks[i].Sha256,
                    FileManifestSha256 = clientComputedHash,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk);
            }

            // TODO: Get rid of this (or not?)
            using var blob = _storage.GetBlobStream(request.ChunkHashes);
            byte[] computedHash = await SHA256.HashDataAsync(blob);
            if (!computedHash.SequenceEqual(newFile.Sha256))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the whole uploaded file.");
            }

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
            var dto = newNodeFile.Adapt<NodeFileManifestDto>();
            dto.ReadMetadataFromManifest(newFile);
            return Ok();
        }
    }
}
