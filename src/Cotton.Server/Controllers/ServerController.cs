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
using Cotton.Storage.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Extensions;
using EasyExtensions.Models.Enums;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Diagnostics;

namespace Cotton.Server.Controllers
{
    [ApiController]
    [Route(Routes.V1.Server)]
    public class ServerController(
        CottonDbContext _dbContext,
        IStoragePipeline _storage,
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
            [FromQuery] bool includePhysicalStorageScan = false,
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

            var gcChunksQuery = _dbContext.Chunks
                .AsNoTracking()
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter < rangeEndUtc
                    && !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash))
                .Select(c => new
                {
                    ScheduledAfter = c.GCScheduledAfter!.Value,
                    c.SizeBytes,
                })
                .AsAsyncEnumerable();

            Dictionary<DateTime, (long ChunkCount, long SizeBytes)> bucketsMap = [];
            await foreach (var item in gcChunksQuery.WithCancellation(cancellationToken))
            {
                DateTime clamped = item.ScheduledAfter < rangeStartUtc ? rangeStartUtc : item.ScheduledAfter;
                DateTime local = clamped.AddMinutes(timezoneOffsetMinutes);
                DateTime localBucketStart = normalizedBucket == "day"
                    ? new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                    : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
                DateTime bucketUtc = DateTime.SpecifyKind(localBucketStart.AddMinutes(-timezoneOffsetMinutes), DateTimeKind.Utc);

                if (!bucketsMap.TryGetValue(bucketUtc, out var existing))
                {
                    bucketsMap[bucketUtc] = (1, item.SizeBytes);
                }
                else
                {
                    bucketsMap[bucketUtc] = (existing.ChunkCount + 1, existing.SizeBytes + item.SizeBytes);
                }
            }

            List<GcChunkTimelineBucketDto> buckets = bucketsMap
                .OrderBy(x => x.Key)
                .Select(x => new GcChunkTimelineBucketDto
                {
                    BucketStartUtc = x.Key,
                    ChunkCount = x.Value.ChunkCount,
                    SizeBytes = x.Value.SizeBytes,
                })
                .ToList();

            long totalChunks = buckets.Sum(x => x.ChunkCount);
            long totalSizeBytes = buckets.Sum(x => x.SizeBytes);
            var storageStats = await GetStorageUsageStatsAsync(now, includePhysicalStorageScan, cancellationToken);

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
                Storage = storageStats,
            });
        }

        private async Task<StorageUsageStatsDto> GetStorageUsageStatsAsync(
            DateTime nowUtc,
            bool includePhysicalStorageScan,
            CancellationToken cancellationToken)
        {
            var chunks = _dbContext.Chunks.AsNoTracking();

            long totalUniqueChunkCount = await chunks.LongCountAsync(cancellationToken);
            long totalUniqueChunkPlainSizeBytes = await chunks.SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0L;

            var referenced = _dbContext.FileManifestChunks
                .AsNoTracking()
                .GroupBy(x => x.ChunkHash)
                .Select(g => new
                {
                    ChunkHash = g.Key,
                    RefCount = g.LongCount(),
                });

            long referencedUniqueChunkCount = await referenced.LongCountAsync(cancellationToken);
            long referencedLogicalChunkCount = await referenced.SumAsync(x => (long?)x.RefCount, cancellationToken) ?? 0L;
            long deduplicatedUniqueChunkCount = await referenced.Where(x => x.RefCount > 1).LongCountAsync(cancellationToken);

            long referencedUniqueChunkPlainSizeBytes = await (
                from c in chunks
                join r in referenced on c.Hash equals r.ChunkHash
                select (long?)c.SizeBytes)
                .SumAsync(cancellationToken) ?? 0L;

            long referencedLogicalPlainSizeBytes = await (
                from c in chunks
                join r in referenced on c.Hash equals r.ChunkHash
                select (long?)(c.SizeBytes * r.RefCount))
                .SumAsync(cancellationToken) ?? 0L;

            var pendingGc = chunks
                .Where(c => c.GCScheduledAfter != null
                    && !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash || fm.LargeFilePreviewHash == c.Hash));

            long pendingGcChunkCount = await pendingGc.LongCountAsync(cancellationToken);
            long pendingGcSizeBytes = await pendingGc.SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0L;

            long overdueGcChunkCount = await pendingGc
                .Where(c => c.GCScheduledAfter <= nowUtc)
                .LongCountAsync(cancellationToken);
            long overdueGcSizeBytes = await pendingGc
                .Where(c => c.GCScheduledAfter <= nowUtc)
                .SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0L;

            long dedupSavedBytes = Math.Max(0, referencedLogicalPlainSizeBytes - referencedUniqueChunkPlainSizeBytes);

            long? physicalStoredObjectCount = null;
            long? physicalStoredSizeBytes = null;
            long? compressionGainBytes = null;
            long? physicalStorageScanDurationMs = null;
            int physicalStorageScanErrors = 0;

            if (includePhysicalStorageScan)
            {
                var stopwatch = Stopwatch.StartNew();
                var (ObjectCount, SizeBytes, Errors) = await ScanPhysicalStorageAsync(cancellationToken);
                stopwatch.Stop();

                physicalStoredObjectCount = ObjectCount;
                physicalStoredSizeBytes = SizeBytes;
                physicalStorageScanErrors = Errors;
                physicalStorageScanDurationMs = stopwatch.ElapsedMilliseconds;
                compressionGainBytes = totalUniqueChunkPlainSizeBytes - SizeBytes;
            }

            return new StorageUsageStatsDto
            {
                StorageType = _settings.GetServerSettings().StorageType.ToString(),
                TotalUniqueChunkCount = totalUniqueChunkCount,
                TotalUniqueChunkPlainSizeBytes = totalUniqueChunkPlainSizeBytes,
                ReferencedUniqueChunkCount = referencedUniqueChunkCount,
                ReferencedUniqueChunkPlainSizeBytes = referencedUniqueChunkPlainSizeBytes,
                ReferencedLogicalChunkCount = referencedLogicalChunkCount,
                ReferencedLogicalPlainSizeBytes = referencedLogicalPlainSizeBytes,
                DeduplicatedUniqueChunkCount = deduplicatedUniqueChunkCount,
                DedupSavedBytes = dedupSavedBytes,
                PendingGcChunkCount = pendingGcChunkCount,
                PendingGcSizeBytes = pendingGcSizeBytes,
                OverdueGcChunkCount = overdueGcChunkCount,
                OverdueGcSizeBytes = overdueGcSizeBytes,
                PhysicalStorageScanCompleted = includePhysicalStorageScan,
                PhysicalStoredObjectCount = physicalStoredObjectCount,
                PhysicalStoredSizeBytes = physicalStoredSizeBytes,
                CompressionGainBytes = compressionGainBytes,
                PhysicalStorageScanDurationMs = physicalStorageScanDurationMs,
                PhysicalStorageScanErrors = physicalStorageScanErrors,
            };
        }

        private async Task<(long ObjectCount, long SizeBytes, int Errors)> ScanPhysicalStorageAsync(CancellationToken cancellationToken)
        {
            long objectCount = 0;
            long sizeBytes = 0;
            int errors = 0;

            int maxDegree = Math.Clamp(Environment.ProcessorCount, 4, 32);
            ParallelOptions options = new()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegree,
            };

            await Parallel.ForEachAsync(_storage.ListAllKeysAsync(cancellationToken), options, async (uid, ct) =>
            {
                try
                {
                    long size = await _storage.GetSizeAsync(uid);
                    Interlocked.Increment(ref objectCount);
                    Interlocked.Add(ref sizeBytes, size);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });

            return (objectCount, sizeBytes, errors);
        }
    }
}
