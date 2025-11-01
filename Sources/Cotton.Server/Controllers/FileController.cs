// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Cotton.Server.Validators;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Models.Requests;
using Cotton.Server.Database.Models;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext, IStorage _storage) : ControllerBase
    {
        [Authorize]
        [HttpDelete($"{Routes.Files}/{{fileManifestId:guid}}")]
        public async Task<IActionResult> DeleteFile([FromRoute] Guid fileManifestId)
        {
            var manifest = await _dbContext.FileManifests.FindAsync(fileManifestId)
                ?? throw new EntityNotFoundException(nameof(FileManifest));
            _dbContext.FileManifests.Remove(manifest);
            await _dbContext.SaveChangesAsync();
            // TODO: Consider deleting or just dereferencing chunks that are no longer used by any file manifests
            return NoContent();
        }        

        // TODO: Authorization: Ensure the user has access to this file
        [HttpGet($"{Routes.Files}/{{fileManifestId:guid}}/download")]
        public async Task<IActionResult> DownloadFile([FromRoute] Guid fileManifestId)
        {
            var manifest = await _dbContext.FileManifests.SingleOrDefaultAsync(x => x.Id == fileManifestId);
            if (manifest == null)
            {
                return CottonResult.NotFound("File manifest not found");
            }
            string[] hashes = await _dbContext.FileManifestChunks
                .Where(x => x.FileManifestId == fileManifestId)
                .OrderBy(x => x.ChunkOrder)
                .Select(x => Convert.ToHexString(x.ChunkSha256))
                .ToArrayAsync();
            Stream stream = _storage.GetBlobStream(hashes);
            return File(stream, manifest.ContentType, manifest.Name);
        }

        [Authorize]
        [HttpPost(Routes.Files)]
        public async Task<IActionResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            Guid userId = User.GetUserId();
            var node = await _dbContext.UserLayoutNodes
                .Where(x => x.Id == request.NodeId)
                .SingleOrDefaultAsync();
            if (node == null)
            {
                return CottonResult.NotFound("User layout node not found.");
            }
            if (node.OwnerId != User.GetUserId())
            {
                return CottonResult.Forbidden("You do not have permission to add files to this node.");
            }

            List<Chunk> chunks = [];
            foreach (var item in request.ChunkHashes)
            {
                var foundChunk = await _dbContext.FindChunkAsync(item);
                if (foundChunk == null)
                {
                    // TODO: Add safety check to ensure chunks belong to the user
                    // Must depend on owner/user authentication, no reason to delay for the same user
                    return CottonResult.BadRequest($"Chunk with hash {item} not found.");
                }
                chunks.Add(foundChunk);
            }

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalizedName, out string errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest($"Invalid file name: {errorMessage}");
            }

            FileManifest newFile = new()
            {
                OwnerId = userId,
                Name = normalizedName,
                ContentType = request.ContentType,
                SizeBytes = chunks.Sum(x => x.SizeBytes),
                Sha256 = Convert.FromHexString(request.Sha256),
            };
            await _dbContext.FileManifests.AddAsync(newFile);

            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    FileManifest = newFile,
                    ChunkSha256 = chunks[i].Sha256,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk);
            }

            // TODO: Get rid of this
            using var blob = _storage.GetBlobStream(request.ChunkHashes);
            byte[] computedHash = await SHA256.HashDataAsync(blob);
            if (!computedHash.SequenceEqual(newFile.Sha256))
            {
                // Rollback
                _dbContext.FileManifestChunks.RemoveRange(
                    _dbContext.FileManifestChunks.Where(x => x.FileManifestId == newFile.Id));
                _dbContext.FileManifests.Remove(newFile);
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the whole uploaded file.");
            }

            NodeFile newNodeFile = new()
            {
                Node = node,
                FileManifest = newFile,
            };
            await _dbContext.UserLayoutNodeFiles.AddAsync(newNodeFile);

            await _dbContext.SaveChangesAsync();
            return Ok(newFile.Adapt<FileManifestDto>());
        }
    }
}
