// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Models;
using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Jobs;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Extensions;
using EasyExtensions.Models.Enums;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Data;
using System.Diagnostics;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Server)]
    public class ServerController(
        CottonDbContext _dbContext,
        SettingsProvider _settings,
        ISchedulerFactory _scheduler,
        IDatabaseBackupManifestService _backupManifestService) : ControllerBase
    {
        private const int DefaultGcTimelineHorizonDays = 30;
        private const int MaxGcTimelineHorizonDays = 365;

        [HttpPost("emergency-shutdown")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public IActionResult EmergencyShutdown()
        {
            Environment.Exit(1);
            return Ok();
        }

        [HttpGet("info")]
        public async Task<IActionResult> GetServerInfo()
        {
            string instanceIdHash = _settings.GetServerSettings().GetInstanceIdHash();
            bool serverHasUsers = await _settings.ServerHasUsersAsync();
            return Ok(new PublicServerInfo()
            {
                // TODO: Change to token-based approach
                InstanceIdHash = instanceIdHash,

                CanCreateInitialAdmin = !serverHasUsers,
                Product = Constants.ProductName,
            });
        }

        [HttpGet("settings/is-setup-complete")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> IsServerInitialized()
        {
            bool isServerInitialized = await _settings.IsServerInitializedAsync();
            return Ok(new { IsServerInitialized = isServerInitialized });
        }

        [HttpPost("settings")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> CreateSettings(ServerSettingsRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.PublicBaseUrl))
            {
                request.PublicBaseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            }
            string? error = await _settings.ValidateServerSettingsAsync(request);
            if (error is not null)
            {
                return this.ApiBadRequest(error);
            }
            await _settings.SaveServerSettingsAsync(request);
            return Ok();
        }

        [Authorize]
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings()
        {
            bool isAdmin = User.IsInRole(nameof(UserRole.Admin));
            int maxChunkSizeBytes = _settings.GetServerSettings().MaxChunkSizeBytes;
            var settings = new
            {
                maxChunkSizeBytes,
                Hasher.SupportedHashAlgorithm,
                settings = isAdmin ? _settings.GetServerSettings() : null,
            };
            return Ok(settings);
        }

        [HttpPatch("database-backup/trigger")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> TriggerDatabaseBackup()
        {
            await _scheduler.TriggerJobAsync<DumpDatabaseJob>();
            return Ok();
        }

        [HttpGet("database-backup/latest")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetLatestDatabaseBackupInfo(CancellationToken cancellationToken)
        {
            ResolvedBackupManifest? backup = await _backupManifestService.TryGetLatestManifestAsync(cancellationToken);
            if (backup is null)
            {
                return NotFound();
            }

            return Ok(new LatestDatabaseBackupDto
            {
                BackupId = backup.Manifest.BackupId,
                CreatedAtUtc = backup.Manifest.CreatedAtUtc,
                PointerUpdatedAtUtc = backup.Pointer.UpdatedAtUtc,
                DumpSizeBytes = backup.Manifest.DumpSizeBytes,
                ChunkCount = backup.Manifest.ChunkCount,
                DumpContentHash = backup.Manifest.DumpContentHash,
                SourceDatabase = backup.Manifest.SourceDatabase,
                SourceHost = backup.Manifest.SourceHost,
                SourcePort = backup.Manifest.SourcePort,
            });
        }

        [HttpGet("gc/chunks/timeline")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> GetGcChunksTimeline(
            [FromQuery] DateTime? fromUtc,
            [FromQuery] DateTime? toUtc,
            [FromQuery] string bucket = "hour",
            [FromQuery] int timezoneOffsetMinutes = 0,
            CancellationToken cancellationToken = default)
        {
            string normalizedBucket = bucket.Trim().ToLowerInvariant();
            if (normalizedBucket is not ("hour" or "day"))
            {
                return this.ApiBadRequest("Invalid bucket value. Supported values: 'hour', 'day'.");
            }

            if (timezoneOffsetMinutes is < -840 or > 840)
            {
                return this.ApiBadRequest("timezoneOffsetMinutes must be between -840 and 840.");
            }

            DateTime now = DateTime.UtcNow;
            DateTime rangeStartUtc = (fromUtc ?? now).ToUniversalTime();
            DateTime rangeEndUtc = (toUtc ?? rangeStartUtc.AddDays(DefaultGcTimelineHorizonDays)).ToUniversalTime();

            if (rangeEndUtc <= rangeStartUtc)
            {
                return this.ApiBadRequest("toUtc must be greater than fromUtc.");
            }

            if (rangeEndUtc > rangeStartUtc.AddDays(MaxGcTimelineHorizonDays))
            {
                return this.ApiBadRequest($"Requested range is too large. Maximum is {MaxGcTimelineHorizonDays} days.");
            }

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    t.bucket_utc,
                    COUNT(*)::bigint AS chunk_count,
                    COALESCE(SUM(t.size_bytes), 0)::bigint AS size_bytes
                FROM (
                    SELECT
                        (date_trunc(@bucket, GREATEST(c.gc_scheduled_after, @from_utc) + make_interval(mins => @timezone_offset_minutes))
                         - make_interval(mins => @timezone_offset_minutes)) AS bucket_utc,
                        c.size_bytes
                    FROM chunks c
                    WHERE c.gc_scheduled_after IS NOT NULL
                      AND c.gc_scheduled_after < @to_utc
                      AND NOT EXISTS (
                          SELECT 1
                          FROM file_manifest_chunks fmc
                          WHERE fmc.chunk_hash = c.hash
                      )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM file_manifests fm
                          WHERE fm.small_file_preview_hash = c.hash OR fm.large_file_preview_hash = c.hash
                      )
                ) t
                GROUP BY t.bucket_utc
                ORDER BY t.bucket_utc;
                """;

            static IDbDataParameter AddParameter(IDbCommand dbCommand, string name, object value)
            {
                var parameter = dbCommand.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                dbCommand.Parameters.Add(parameter);
                return parameter;
            }

            AddParameter(command, "bucket", normalizedBucket);
            AddParameter(command, "timezone_offset_minutes", timezoneOffsetMinutes);
            AddParameter(command, "from_utc", rangeStartUtc);
            AddParameter(command, "to_utc", rangeEndUtc);

            List<GcChunkTimelineBucketDto> buckets = [];
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    buckets.Add(new GcChunkTimelineBucketDto
                    {
                        BucketStartUtc = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                        ChunkCount = reader.GetInt64(1),
                        SizeBytes = reader.GetInt64(2),
                    });
                }
            }

            long totalChunks = buckets.Sum(x => x.ChunkCount);
            long totalSizeBytes = buckets.Sum(x => x.SizeBytes);

            return Ok(new GcChunkTimelineDto
            {
                Bucket = normalizedBucket,
                TimezoneOffsetMinutes = timezoneOffsetMinutes,
                FromUtc = rangeStartUtc,
                ToUtc = rangeEndUtc,
                GeneratedAtUtc = now,
                TotalChunks = totalChunks,
                TotalSizeBytes = totalSizeBytes,
                Buckets = buckets,
            });
        }
    }
}
