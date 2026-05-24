// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Models;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Crypto;
using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for chunk operations.
    /// </summary>
    public class ChunkController(
        PerfTracker _perf,
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        ILogger<ChunkController> _logger,
        IChunkIngestService _chunkIngest,
        IStoragePipeline _storage) : ControllerBase
    {
        /// <summary>
        /// Checks whether a chunk hash is already stored.
        /// </summary>
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

            bool existsInStorage = await _storage.ExistsAsync(hash);
            return Ok(existsInStorage);
        }

        /// <summary>
        /// Uploads a raw content-addressed chunk.
        /// </summary>
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
            Guid userId = User.GetUserId();
            try
            {
                await _chunkIngest.UpsertChunkAsync(userId, stream, file.Length, hashBytes);
            }
            catch (InvalidDataException ex)
            {
                return CottonResult.BadRequest(ex.Message);
            }
            catch (StoragePressureException ex)
            {
                _logger.LogWarning(ex, "Rejected chunk upload because storage free space is below the configured reserve.");
                return StatusCode(507, "Storage is running out of free space. Uploads are temporarily paused.");
            }

            _logger.LogDebug("Stored chunk {Hash} of size {Size} bytes", hash, file.Length);
            _perf.OnChunkCreated();
            return Created();
        }

        /// <summary>
        /// Uploads a raw content-addressed chunk without multipart form parsing.
        /// </summary>
        [Authorize]
        [HttpPost(Routes.V1.Chunks + "/raw")]
        [RequestSizeLimit(AesGcmStreamCipher.MaxChunkSize)]
        public async Task<IActionResult> UploadRawChunk([FromQuery] string hash)
        {
            long? contentLength = Request.ContentLength;
            if (!contentLength.HasValue || contentLength.Value <= 0)
            {
                return CottonResult.BadRequest("No file uploaded.");
            }

            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            if (contentLength.Value > maxChunkSizeBytes)
            {
                return CottonResult.BadRequest($"File size exceeds maximum chunk size of {maxChunkSizeBytes} bytes.");
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

            Guid userId = User.GetUserId();
            try
            {
                await _chunkIngest.UpsertChunkAsync(userId, Request.Body, contentLength.Value, hashBytes, HttpContext.RequestAborted);
            }
            catch (InvalidDataException ex)
            {
                return CottonResult.BadRequest(ex.Message);
            }
            catch (StoragePressureException ex)
            {
                _logger.LogWarning(ex, "Rejected raw chunk upload because storage free space is below the configured reserve.");
                return StatusCode(507, "Storage is running out of free space. Uploads are temporarily paused.");
            }

            _logger.LogDebug("Stored raw chunk {Hash} of size {Size} bytes", hash, contentLength.Value);
            _perf.OnChunkCreated();
            return Created();
        }
    }
}
