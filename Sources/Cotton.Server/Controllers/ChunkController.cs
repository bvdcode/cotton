using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Settings;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Cotton.Server.Database.Models;

namespace Cotton.Server.Controllers
{
    public class ChunkController(CottonDbContext _dbContext, CottonSettings _settings, IStorage _storage) : ControllerBase
    {
        [HttpPost(Routes.Chunks)]
        public async Task<IActionResult> UploadChunk(IFormFile file, string hash)
        {
            if (file == null || file.Length == 0)
            {
                return CottonResult.BadRequest("No file uploaded.");
            }
            if (file.Length > _settings.ChunkSizeBytes)
            {
                return CottonResult.BadRequest($"File size exceeds maximum chunk size of {_settings.ChunkSizeBytes} bytes.");
            }
            if (string.IsNullOrWhiteSpace(hash))
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }

            byte[] hashBytes = Convert.FromHexString(hash);
            if (hashBytes.Length != SHA256.HashSizeInBytes)
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }

            await using var stream = file.OpenReadStream();
            await using var tmp = new MemoryStream(capacity: (int)file.Length);
            await stream.CopyToAsync(tmp);

            var existingChunk = await _dbContext.Chunks.FindAsync(hashBytes);
            if (existingChunk != null)
            {
                // TODO: Verify if it's safe to return OK here without ownership checks. I think it is, because writing the chunk is very fast.
                await _storage.WriteChunkAsync(hash, tmp);
                return CottonResult.Ok("Chunk was uploaded successfully.");
            }
            byte[] computedHash = await SHA256.HashDataAsync(tmp);
            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            var chunk = new Chunk
            {
                Sha256 = hashBytes,
            };
            await _dbContext.Chunks.AddAsync(chunk);
            await _dbContext.SaveChangesAsync();

            tmp.Seek(default, SeekOrigin.Begin);
            try
            {
                await _storage.WriteChunkAsync(hash, tmp);
                return CottonResult.Ok("Chunk was uploaded successfully.");
            }
            catch (Exception ex)
            {
                // Rollback DB entry if storage write fails
                _dbContext.Chunks.Remove(chunk);
                await _dbContext.SaveChangesAsync();
                return CottonResult.InternalError("Failed to store the uploaded chunk.", ex);
            }
        }
    }
}
