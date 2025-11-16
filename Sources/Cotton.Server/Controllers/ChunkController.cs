// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Cotton.Shared;
using Cotton.Crypto;
using EasyExtensions;
using Cotton.Topology;
using Cotton.Database;
using Cotton.Server.Models;
using Cotton.Database.Models;
using Microsoft.AspNetCore.Mvc;
using Cotton.Storage.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using System.Diagnostics;

namespace Cotton.Server.Controllers
{
    public class ChunkController(CottonDbContext _dbContext, CottonSettings _settings,
        IStoragePipeline _storage, ILogger<ChunkController> _logger, StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpGet(Routes.Chunks + "/{hash}")]
        public async Task<IActionResult> GetChunk([FromRoute] string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }
            byte[] hashBytes = Convert.FromHexString(hash);
            if (hashBytes.Length != Hasher.HashSizeInBytes)
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }
            Guid userId = User.GetUserId();
            var chunkOwnership = await _dbContext.ChunkOwnerships
                .FirstOrDefaultAsync(co => co.ChunkHash.SequenceEqual(hashBytes)
                    && co.OwnerId == userId);
            if (chunkOwnership == null)
            {
                return this.ApiNotFound("Chunk not found or access denied.");
            }
            var chunk = chunkOwnership.Chunk;
            return Ok();
        }

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
            if (hashBytes.Length != Hasher.HashSizeInBytes)
            {
                return CottonResult.BadRequest("Invalid hash format.");
            }

            Stopwatch sw = Stopwatch.StartNew();
            _logger.LogInformation("Uploading chunk {Hash} of size {Size} bytes.", hash, file.Length);
            using var stream = file.OpenReadStream();
            byte[] computedHash = await Hasher.HashDataAsync(stream);
            _logger.LogInformation("Computed hash for uploaded chunk {Hash} in {ElapsedMilliseconds} ms.", hash, sw.ElapsedMilliseconds);
            sw.Restart();
            stream.Seek(default, SeekOrigin.Begin);
            if (!computedHash.SequenceEqual(hashBytes))
            {
                return CottonResult.BadRequest("Hash mismatch: the provided hash does not match the uploaded file.");
            }

            var chunk = await _layouts.FindChunkAsync(hashBytes);
            if (chunk == null)
            {
                await _storage.WriteAsync(hash, stream);
                chunk = new Chunk
                {
                    Hash = hashBytes,
                    SizeBytes = file.Length,
                };
                await _dbContext.Chunks.AddAsync(chunk);
                _logger.LogInformation("Stored chunk {Hash} in storage backend in {ElapsedMilliseconds} ms.", hash, sw.ElapsedMilliseconds);
                sw.Restart();
            }
            // TODO: Add Simulated Write Delay to prevent Proof-of-Storage attacks
            // Must depend on owner/user authentication, no reason to delay for the same user
            var foundOwnership = await _dbContext.ChunkOwnerships
                .FirstOrDefaultAsync(co => co.ChunkHash.SequenceEqual(hashBytes)
                    && co.OwnerId == User.GetUserId());
            if (foundOwnership == null)
            {
                ChunkOwnership chunkOwnership = new()
                {
                    ChunkHash = hashBytes,
                    OwnerId = User.GetUserId(),
                };
                await _dbContext.ChunkOwnerships.AddAsync(chunkOwnership);
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Stored new chunk {Hash} of size {Size} bytes for user {UserId} in {ElapsedMilliseconds} ms.", hash, file.Length, User.GetUserId(), sw.ElapsedMilliseconds);
            return Created();
        }
    }
}
