using Mapster;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Extensions;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Validators;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext, IStorage _storage) : ControllerBase
    {
        [HttpDelete(Routes.Files + "/{fileManifestId:guid}")]
        public async Task<CottonResult> DeleteFile([FromRoute] Guid fileManifestId)
        {
            var manifest = await _dbContext.FileManifests.FindAsync(fileManifestId)
                ?? throw new EntityNotFoundException(nameof(FileManifest));
            _dbContext.FileManifests.Remove(manifest);
            await _dbContext.SaveChangesAsync();
            // TODO: Consider deleting unreferenced chunks
            return CottonResult.Ok("File deleted successfully.");
        }

        [HttpGet(Routes.Files)]
        public async Task<CottonResult> GetFiles()
        {
            var all = await _dbContext.FileManifests.ToListAsync();
            var mapped = all.Adapt<FileManifestDto[]>();
            return CottonResult.Ok("Files retrieved successfully.", mapped);
        }

        [HttpGet(Routes.Files + "/{fileManifestId:guid}/download")]
        public async Task<IActionResult> DownloadFile([FromRoute] Guid fileManifestId)
        {
            var manifest = await _dbContext.FileManifests.FindAsync(fileManifestId)
                ?? throw new EntityNotFoundException(nameof(FileManifest));
            string[] hashes = await _dbContext.FileManifestChunks
                .Where(x => x.FileManifestId == fileManifestId)
                .OrderBy(x => x.ChunkOrder)
                .Select(x => Convert.ToHexString(x.ChunkSha256))
                .ToArrayAsync();
            Stream stream = _storage.GetBlobStream(hashes);
            return File(stream, manifest.ContentType, manifest.Name);
        }

        [HttpPost(Routes.Files)]
        public async Task<CottonResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
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
                Sha256 = Convert.FromHexString(request.Sha256),
                SizeBytes = chunks.Sum(x => x.SizeBytes)
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

            await _dbContext.SaveChangesAsync();
            return CottonResult.Ok("File created successfully.", newFile.Adapt<FileManifestDto>());
        }
    }
}
