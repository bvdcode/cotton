using Cotton.Server.Abstractions;
using Cotton.Server.Database;
using Cotton.Server.Database.Models;
using Cotton.Server.Extensions;
using Cotton.Server.Models;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Validators;
using EasyExtensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext, IStorage _storage) : ControllerBase
    {
        [Authorize]
        [HttpDelete($"{Routes.Files}/{{fileManifestId:guid}}")]
        public async Task<CottonResult> DeleteFile([FromRoute] Guid fileManifestId)
        {
            var manifest = await _dbContext.FileManifests.FindAsync(fileManifestId)
                ?? throw new EntityNotFoundException(nameof(FileManifest));
            _dbContext.FileManifests.Remove(manifest);
            await _dbContext.SaveChangesAsync();
            // TODO: Consider deleting or just dereferencing chunks that are no longer used by any file manifests
            return CottonResult.Ok("File deleted successfully.");
        }
        
        [Authorize]
        [HttpGet(Routes.Files)]
        [Obsolete("Use Layout resolver endpoints instead.")]
        public async Task<CottonResult> GetFiles()
        {
            var all = await _dbContext.FileManifests.ToListAsync();
            var mapped = all.Adapt<FileManifestDto[]>();
            return CottonResult.Ok("Files retrieved successfully.", mapped);
        }

        // TODO: Authorization: Ensure the user has access to this file
        [HttpGet($"{Routes.Files}/{{fileManifestId:guid}}/download")]
        public async Task<IActionResult> DownloadFile([FromRoute] Guid fileManifestId)
        {
            Guid userId = User.GetUserId();
            var manifest = await _dbContext.FileManifests.SingleOrDefaultAsync(x => x.Id == fileManifestId && x.OwnerId == userId);
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
        public async Task<CottonResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
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

            bool isValidName = NameValidator.TryNormalizeAndValidate(request.Name, out string normalized, out string errorMessage);
            if (!isValidName)
            {
                return CottonResult.BadRequest($"Invalid file name: {errorMessage}");
            }

            FileManifest newFile = new()
            {
                Name = normalized,
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

            UserLayoutNodeFile newNodeFile = new()
            {
                UserLayoutNode = node,
                FileManifest = newFile,
            };
            await _dbContext.UserLayoutNodeFiles.AddAsync(newNodeFile);

            await _dbContext.SaveChangesAsync();
            return CottonResult.Ok("File created successfully.", newFile.Adapt<FileManifestDto>());
        }
    }
}
