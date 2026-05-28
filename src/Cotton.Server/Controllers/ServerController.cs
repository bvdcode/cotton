// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Models;
using Cotton.Server.Handlers.Server;
using Cotton.Server.Jobs;
using Cotton.Server.Models.Dto;
using Cotton.Server.Models.Requests;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Services.KeyManagement;
using EasyExtensions.Mediator;
using EasyExtensions.Models.Enums;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace Cotton.Server.Controllers
{
    /// <summary>
    /// Exposes HTTP endpoints for server operations.
    /// </summary>
    [ApiController]
    [Route(Routes.V1.Server)]
    public class ServerController(
        IMediator _mediator,
        SettingsProvider _settings,
        ISchedulerFactory _scheduler,
        SecurityDiagnosticsService _securityDiagnostics,
        KeyringAdminService _keyringAdmin) : ControllerBase
    {
        /// <summary>
        /// Stops the server after an authenticated emergency shutdown request.
        /// </summary>
        [HttpPost("emergency-shutdown")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> EmergencyShutdown(CancellationToken cancellationToken)
        {
            await _mediator.Send(new EmergencyShutdownRequest(), cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Gets server info.
        /// </summary>
        [HttpGet("info")]
        public async Task<IActionResult> GetServerInfo()
        {
            string instanceIdHash = _settings.GetServerSettings().GetInstanceIdHash();
            bool serverHasUsers = await _settings.ServerHasUsersAsync();
            return Ok(new PublicServerInfo()
            {
                InstanceIdHash = instanceIdHash,
                CanCreateInitialAdmin = !serverHasUsers,
                Product = Constants.ProductName,
            });
        }

        /// <summary>
        /// Gets security diagnostics.
        /// </summary>
        [HttpGet("security/status")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetSecurityDiagnostics(CancellationToken cancellationToken)
        {
            SecurityDiagnosticsDto snapshot = await _securityDiagnostics.GetSnapshotAsync(cancellationToken);
            return Ok(snapshot);
        }

        /// <summary>
        /// Rotates the keyring unlock secret without rewriting user chunks.
        /// </summary>
        [HttpPost("keyring/rotate-unlock")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> RotateKeyringUnlock(
            [FromBody] KeyringRotateUnlockRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                KeyringAdminRotationResult result = await _keyringAdmin.RotateUnlockAsync(
                    request.CurrentUnlockSecret,
                    request.NewUnlockSecret,
                    cancellationToken);
                return Ok(new KeyringRotateUnlockResponseDto
                {
                    RootEpoch = result.RootEpoch,
                    AccessGeneration = result.AccessGeneration,
                    StateGeneration = result.StateGeneration,
                });
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Exports the encrypted keyring recovery kit.
        /// </summary>
        [HttpGet("keyring/recovery-kit")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> ExportKeyringRecoveryKit(CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _keyringAdmin.ExportRecoveryKitAsync(cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Schedules an immediate database backup.
        /// </summary>
        [HttpPatch("database-backup/trigger")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> TriggerDatabaseBackup(CancellationToken cancellationToken)
        {
            await _mediator.Send(new TriggerDatabaseBackupRequest(), cancellationToken);
            return Ok();
        }

        /// <summary>
        /// Schedules an immediate chunk garbage-collection pass.
        /// </summary>
        [HttpPatch("gc/trigger")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> TriggerGarbageCollector()
        {
            await _scheduler.TriggerJobAsync<GarbageCollectorJob>();
            return Ok();
        }

        /// <summary>
        /// Gets latest database backup info.
        /// </summary>
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

        /// <summary>
        /// Gets gc chunks timeline.
        /// </summary>
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
