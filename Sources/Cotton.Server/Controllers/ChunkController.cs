using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Settings;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Cotton.Server.Database.Models;

namespace Cotton.Server.Controllers
{
    public class ChunkController(CottonDbContext _dbContext, CottonSettings _settings, 
        IStorage _storage, ILogger<ChunkController> _logger) : ControllerBase
    {
        [HttpPost(Routes.Chunks)]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile file, [FromForm] string hash)
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

            using var tmp = WrapIfNonSeekable(file);

            byte[] computedHash = await SHA256.HashDataAsync(tmp);
            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            var chunk = await _dbContext.Chunks.FindAsync(hashBytes);
            if (chunk != null)
            {
                // TODO: Add Simulated Write Delay to prevent Proof-of-Storage attacks
                // Must depend on owner/user authentication, no reason to delay for the same user
                return CottonResult.Ok("Chunk was uploaded successfully.");
            }
            tmp.Seek(default, SeekOrigin.Begin);
            try
            {
                await _storage.WriteChunkAsync(hash, tmp);
                chunk = new Chunk
                {
                    Sha256 = hashBytes,
                    SizeBytes = file.Length,
                };
                await _dbContext.Chunks.AddAsync(chunk);
                await _dbContext.SaveChangesAsync();
                return CottonResult.Ok("Chunk was uploaded successfully.");
            }
            catch (Exception)
            {
                return CottonResult.InternalError("Failed to store the uploaded chunk.");
            }
        }

        private Stream WrapIfNonSeekable(IFormFile file)
        {
            var stream = file.OpenReadStream();
            if (stream.CanSeek)
            {
                _logger.LogDebug("Uploaded file stream is seekable: {name}", file.FileName);
                return stream;
            }
            _logger.LogDebug("Uploaded file stream is NOT seekable: {name}", file.FileName);
            return new MemoryStream(capacity: (int)file.Length);
        }
    }
}
