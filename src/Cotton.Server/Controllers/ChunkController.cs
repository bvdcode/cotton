// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using Cotton.Storage.Processors;
using Cotton.Topology;
using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    public class ChunkController(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        IStoragePipeline _storage,
        ILogger<ChunkController> _logger,
        StorageLayoutService _layouts) : ControllerBase
    {
        [Authorize]
        [HttpGet(Routes.Chunks + "/{hash}/exists")]
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
        [HttpPost(Routes.Chunks)]
        [RequestSizeLimit(16 * 1024 * 1024)]
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile file, [FromForm] string hash)
        {
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
            var foundOwnership = await _dbContext.ChunkOwnerships
                .FirstOrDefaultAsync(co => co.ChunkHash == hashBytes && co.OwnerId == userId);
            string storageKey = Hasher.ToHexStringHash(computedHash);
            var chunk = await _layouts.FindChunkAsync(hashBytes);
            if (chunk == null)
            {
                await _storage.WriteAsync(storageKey, stream, new PipelineContext());
                chunk = new Chunk
                {
                    Hash = hashBytes,
                    SizeBytes = file.Length,
                    CompressionAlgorithm = CompressionProcessor.Algorithm
                };
                await _dbContext.Chunks.AddAsync(chunk);
            }

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
            _logger.LogInformation("Stored new chunk {Hash} of size {Size} bytes", storageKey, file.Length);
            _perf.OnChunkCreated();
            return Created();
        }
    }
}
