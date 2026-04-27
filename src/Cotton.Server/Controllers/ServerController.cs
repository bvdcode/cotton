// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Models;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Runtime;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Server)]
    public class ServerController(
        SettingsProvider _settings,
        IMediator _mediator) : ControllerBase
    {
        [HttpPost("emergency-shutdown")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> EmergencyShutdown(CancellationToken cancellationToken)
        {
            await _mediator.Send(new EmergencyShutdownRequest(), cancellationToken);
            return Ok();
        }

        [HttpGet("info")]
        public async Task<IActionResult> GetServerInfo()
        {
            PublicServerInfo result = await _mediator.Send(new GetServerInfoQuery());
            return Ok(result);
        }

        [HttpPatch("database-backup/trigger")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> TriggerDatabaseBackup(CancellationToken cancellationToken)
        {
            await _mediator.Send(new TriggerDatabaseBackupRequest(), cancellationToken);
            return Ok();
        }

        [HttpGet("database-backup/latest")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetLatestDatabaseBackupInfo(CancellationToken cancellationToken)
        {
            LatestDatabaseBackupDto? result = await _mediator.Send(new GetLatestDatabaseBackupInfoQuery(), cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpGet("gc/chunks/timeline")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetGcChunksTimeline(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string bucket = "hour",
            CancellationToken cancellationToken = default)
        {
            string? timezoneId = Request.Headers["X-Timezone"].FirstOrDefault();
            var result = await _mediator.Send(
                new GetGcChunksTimelineQuery(fromUtc, toUtc, bucket, timezoneId),
                cancellationToken);
            return Ok(result);
        }
    }
}
