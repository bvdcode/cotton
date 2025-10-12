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
            if (file.Length > _settings.MaxChunkSizeBytes)
            {
                return CottonResult.BadRequest($"File size exceeds maximum chunk size of {_settings.MaxChunkSizeBytes} bytes.");
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

            byte[] computedHash = await SHA256.HashDataAsync(tmp);
            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            var chunk = await _dbContext.Chunks.FindAsync(hashBytes);
            chunk ??= new Chunk
            {
                Sha256 = hashBytes,
            };


            tmp.Seek(default, SeekOrigin.Begin);
            try
            {
                await _storage.WriteChunkAsync(hash, tmp);
                await _dbContext.Chunks.AddAsync(chunk);
                await _dbContext.SaveChangesAsync();
                return CottonResult.Ok("Chunk was uploaded successfully.");
            }
            catch (Exception ex)
            {
                return CottonResult.InternalError("Failed to store the uploaded chunk.", ex);
            }
        }
    }
}
