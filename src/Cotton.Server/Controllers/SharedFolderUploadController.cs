// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database.Models;
using Cotton.Files;
using Cotton.Server.Auth;
using Cotton.Server.Extensions;
using Cotton.Server.Handlers.Layouts;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route(Routes.V1.Layouts + "/shared/{token}")]
    public class SharedFolderUploadController(IMediator mediator, PublicShareLookupFailureLimiter lookupFailures,
        ILogger<SharedFolderUploadController> logger) : ControllerBase
    {
        [HttpPost("chunks/raw")]
        [RequestSizeLimit(AesGcmStreamCipher.MaxChunkSize)]
        public async Task<IActionResult> UploadChunk(string token, [FromQuery] Guid nodeId,
            [FromQuery] Guid sessionId, [FromQuery] string hash, CancellationToken ct)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(lookupFailures, token);
            if (blocked is not null)
            {
                return blocked;
            }
            if (Request.ContentLength is not long length)
            {
                return BadRequest("Content length is required.");
            }
            try
            {
                await mediator.Send(new UploadSharedChunkRequest(token, nodeId, sessionId, hash, Request.Body, length), ct);
                return Created();
            }
            catch (EntityNotFoundException<NodeShareToken> exception)
            {
                logger.LogDebug(exception, "Shared upload access is unavailable.");
                return this.ApiPublicShareNotFound(lookupFailures, token, "Shared folder not found.");
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Rejected invalid shared upload chunk.");
                return BadRequest("Invalid upload chunk.");
            }
            catch (StoragePressureException exception)
            {
                logger.LogWarning(exception, "Shared upload rejected because storage free space is below the configured reserve.");
                return StatusCode(507, "Storage is running out of free space. Uploads are temporarily paused.");
            }
        }

        [HttpPost("files/from-chunks")]
        public async Task<IActionResult> CreateFile(string token, [FromQuery] Guid sessionId,
            [FromBody] CreateFileFromChunksRequestDto file, CancellationToken ct)
        {
            IActionResult? blocked = this.GetPublicShareLookupBlockRejection(lookupFailures, token);
            if (blocked is not null)
            {
                return blocked;
            }
            try
            {
                return Ok(await mediator.Send(new CreateSharedFileRequest(token, sessionId, file), ct));
            }
            catch (EntityNotFoundException<NodeShareToken> exception)
            {
                logger.LogDebug(exception, "Shared upload access is unavailable.");
                return this.ApiPublicShareNotFound(lookupFailures, token, "Shared folder not found.");
            }
        }
    }
}
