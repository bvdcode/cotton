// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Sync;
using Cotton.Server.Models.Dto;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cotton.Server.Controllers
{
    /// <summary>Exposes durable synchronization endpoints.</summary>
    [ApiController]
    [Authorize]
    [Route(Routes.V1.Sync)]
    public sealed class SyncController(IMediator _mediator) : ControllerBase
    {
        /// <summary>Returns ordered remote mutations after the supplied cursor.</summary>
        [HttpGet("changes")]
        [ProducesResponseType<SyncChangesResponseDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetChanges(
            [FromQuery] long since = 0,
            [FromQuery] int limit = 500)
        {
            if (since < 0)
            {
                return this.ApiBadRequest("Cursor must be greater than or equal to zero.");
            }

            Guid userId = User.GetUserId();
            SyncChangesResponseDto response = await _mediator.Send(new GetSyncChangesQuery(userId, since, limit));
            return Ok(response);
        }
    }
}
