// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Sync;
using Cotton.Server.Models.Dto;
using EasyExtensions.Helpers;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for synchronization clients.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route(Routes.V1.Sync)]
    public class SyncController(IMediator _mediator) : ControllerBase
    {
        /// <summary>
        /// Gets durable file-tree changes after the supplied cursor.
        /// </summary>
        [HttpGet("changes")]
        public async Task<ActionResult<SyncChangesResponseDto>> GetChanges(
            [FromQuery] long since = 0,
            [FromQuery] int limit = 500,
            CancellationToken cancellationToken = default)
        {
            Guid userId = User.GetUserId();
            SyncChangesResponseDto response = await _mediator.Send(
                new GetSyncChangesQuery(userId, since, limit),
                cancellationToken);

            return Ok(response);
        }
    }
}
