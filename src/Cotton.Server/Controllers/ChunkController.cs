// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Shared;
using EasyExtensions;
using EasyExtensions.Crypto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    public class ChunkController(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        ILogger<ChunkController> _logger,
        IChunkIngestService _chunkIngest) : ControllerBase
    {
        [Authorize]
        [HttpGet(Routes.V1.Chunks + "/{hash}/exists")]
        public async Task<IActionResult> CheckChunkExists([FromRoute] string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }
            byte[] hashBytes = Hasher.FromHexStringHash(hash);
            if (hashBytes.Length != Hasher.HashSizeInBytes)
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }
            Guid userId = User.GetUserId();
            var chunkOwnership = await _dbContext.ChunkOwnerships
                .FirstOrDefaultAsync(co =>
                    co.ChunkHash == hashBytes &&
                    co.OwnerId == userId);
            if (chunkOwnership == null)
            {
                return Ok(false);
            }
            return Ok(true);
        }

        [Authorize]
        [HttpPost(Routes.V1.Chunks)]
        [RequestSizeLimit(AesGcmStreamCipher.MaxChunkSize + ushort.MaxValue)]
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile file, [FromForm] string hash)
        {
            // TODO: Add streaming upload without IFormFile
            // write under client-provided hash and validate on-the-fly
            // reject and delete from storage if hash does not match
            // it gives no allocations in /tmp and is generally more efficient
            // but now it's more complex to implement
            // Don't forget - if hash mismatches I can't just delete the chunk from storage
            // it may be owned by other users or it might be hacked together from other chunks

            if (file == null || file.Length == 0)
            {
                return CottonResult.BadRequest("No file uploaded.");
            }
            if (file.Length > _settings.GetServerSettings().MaxChunkSizeBytes)
            {
                return CottonResult.BadRequest($"File size exceeds maximum chunk size of {_settings.GetServerSettings().MaxChunkSizeBytes} bytes.");
            }
            if (string.IsNullOrWhiteSpace(hash))
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }

            byte[] hashBytes = Hasher.FromHexStringHash(hash);
            if (hashBytes.Length != Hasher.HashSizeInBytes)
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }

            using var stream = file.OpenReadStream();
            byte[] computedHash = await Hasher.HashDataAsync(stream);
            stream.Seek(default, SeekOrigin.Begin);

            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            Guid userId = User.GetUserId();
            await _chunkIngest.UpsertChunkAsync(userId, stream, file.Length);

            _logger.LogInformation("Stored chunk {Hash} of size {Size} bytes", hash, file.Length);
            _perf.OnChunkCreated();
            return Created();
        }
    }
}
