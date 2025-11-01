// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Server.Models;
using Cotton.Server.Database;
using Cotton.Server.Services;
using Cotton.Server.Settings;
using Microsoft.AspNetCore.Mvc;
using Cotton.Server.Abstractions;
using System.Security.Cryptography;
using Cotton.Server.Database.Models;
using Microsoft.AspNetCore.Authorization;

namespace Cotton.Server.Controllers
{
    public class ChunkController(CottonDbContext _dbContext, CottonSettings _settings, 
        IStorage _storage, ILogger<ChunkController> _logger, StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpPost(Routes.Chunks)]
        [RequestSizeLimit(100 * 1024 * 1024)]
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

            using var stream = file.OpenReadStream();
            byte[] computedHash = await SHA256.HashDataAsync(stream);
            stream.Seek(default, SeekOrigin.Begin);
            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            var chunk = await _layouts.FindChunkAsync(hashBytes);
            if (chunk != null)
            {
                // TODO: Add Simulated Write Delay to prevent Proof-of-Storage attacks
                // Must depend on owner/user authentication, no reason to delay for the same user
                return Created();
            }
            try
            {
                await _storage.WriteFileAsync(hash, stream);
                chunk = new Chunk
                {
                    Sha256 = hashBytes,
                    SizeBytes = file.Length,
                };
                await _dbContext.Chunks.AddAsync(chunk);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Stored new chunk {Hash} of size {Size} bytes.", hash, chunk.SizeBytes);
                return Created();
            }
            catch (Exception)
            {
                return CottonResult.InternalError("Failed to store the uploaded chunk.");
            }
        }
    }
}
