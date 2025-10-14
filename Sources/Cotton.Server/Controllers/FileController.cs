using Mapster;
using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Models.Dto;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Requests;
using Microsoft.EntityFrameworkCore;
using Cotton.Server.Database.Models;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Cotton.Server.Controllers
{
    [ApiController]
    public class FileController(CottonDbContext _dbContext, IStorage _storage) : ControllerBase
    {
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
            return File(stream, manifest.ContentType);
        }

        [HttpPost(Routes.Files)]
        public async Task<CottonResult> CreateFileFromChunks([FromBody] CreateFileRequest request)
        {
            List<Chunk> chunks = [];
            foreach (var item in request.ChunkHashes)
            {
                var foundChunk = await _dbContext.Chunks.FindAsync(Convert.FromHexString(item));
                if (foundChunk == null)
                {
                    // TODO: Add safety check to ensure chunks belong to the user
                    return CottonResult.BadRequest($"Chunk with hash {item} not found.");
                }
                chunks.Add(foundChunk);
            }

            FileManifest newFile = new()
            {
                ContentType = request.ContentType,
                Folder = request.Folder,
                Name = request.Name,
                Sha256 = Convert.FromHexString(request.Sha256),
                SizeBytes = chunks.Sum(x => x.SizeBytes)
            };
            await _dbContext.FileManifests.AddAsync(newFile);
            await _dbContext.SaveChangesAsync();

            for (int i = 0; i < chunks.Count; i++)
            {
                var fileChunk = new FileManifestChunk
                {
                    ChunkOrder = i,
                    FileManifestId = newFile.Id,
                    ChunkSha256 = chunks[i].Sha256,
                };
                await _dbContext.FileManifestChunks.AddAsync(fileChunk);
            }

            await _dbContext.SaveChangesAsync();

            return CottonResult.Ok("File created successfully.", newFile.Adapt<FileManifestDto>());
        }
    }
}
