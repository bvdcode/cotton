// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Cotton.Topology;
using Cotton.Database;
using Cotton.Validators;
using System.Diagnostics;
using Cotton.Server.Models;
using Cotton.Database.Models;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Storage.Abstractions;
using Cotton.Database.Models.Enums;
using System.Security.Cryptography;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using Cotton.Crypto;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(
        IStorage _storage,
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
                .Select(x => Convert.ToHexString(x.ChunkHash))];
            Stream stream = _storage.GetBlobStream(hashes);
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

            Stopwatch sw = Stopwatch.StartNew();
            // TODO: Get rid of this (or not?)
            using var blob = _storage.GetBlobStream(request.ChunkHashes);
            byte[] computedHash = await Hasher.HashDataAsync(blob);
            _logger.LogInformation("Computed hash for file {FileName} in {ElapsedMilliseconds} ms", request.Name, sw.ElapsedMilliseconds);
            if (!string.IsNullOrWhiteSpace(request.Hash))
            {
                byte[] providedHash = Convert.FromHexString(request.Hash);
                if (!computedHash.SequenceEqual(providedHash))
                {
                    return CottonResult.BadRequest("Provided Hash does not match the computed hash of the file.");
                }
            }

            var newFile = await _dbContext.FileManifests.FindAsync(computedHash);
            if (newFile == null)
            {
                newFile = new FileManifest()
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
