using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using EasyExtensions.AspNetCore.Exceptions;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Server.Handlers.Server
{
    public class GetGcChunksTimelineQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string bucket,
        string? timezoneId) : IRequest<GcChunkTimelineDto>
    {
        public DateTime? FromUtc { get; } = fromUtc;
        public DateTime? ToUtc { get; } = toUtc;
        public string Bucket { get; } = bucket;
        public string? TimezoneId { get; } = timezoneId;
    }

    public class GetGcChunksTimelineQueryHandler(
        CottonDbContext _dbContext,
        SettingsProvider _settings) : IRequestHandler<GetGcChunksTimelineQuery, GcChunkTimelineDto>
    {
        private const int DefaultGcTimelineHorizonDays = 30;
        private const int MaxGcTimelineHorizonDays = 365;

        public async Task<GcChunkTimelineDto> Handle(GetGcChunksTimelineQuery request, CancellationToken cancellationToken)
        {
            string normalizedBucket = request.Bucket.Trim().ToLowerInvariant();
            if (normalizedBucket is not ("hour" or "day"))
            {
                throw new BadRequestException("Invalid bucket value. Supported values: 'hour', 'day'.");
            }

            TimeZoneInfo effectiveTimeZone = ResolveTimelineTimeZone(request.TimezoneId);

            DateTime now = DateTime.UtcNow;
            DateTime rangeStartUtc = (request.FromUtc ?? now).ToUniversalTime();
            DateTime rangeEndUtc = (request.ToUtc ?? rangeStartUtc.AddDays(DefaultGcTimelineHorizonDays)).ToUniversalTime();

            if (rangeEndUtc <= rangeStartUtc)
            {
                throw new BadRequestException("toUtc must be greater than fromUtc.");
            }

            if (rangeEndUtc > rangeStartUtc.AddDays(MaxGcTimelineHorizonDays))
            {
                throw new BadRequestException($"Requested range is too large. Maximum is {MaxGcTimelineHorizonDays} days.");
            }

            var gcBaseQuery = _dbContext.Chunks
                .AsNoTracking()
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter < rangeEndUtc
                    && !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash)
                    && !_dbContext.FileManifests.Any(fm => fm.LargeFilePreviewHash == c.Hash));

            var overdueAggregate = await gcBaseQuery
                .Where(c => c.GCScheduledAfter < rangeStartUtc)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    ChunkCount = g.LongCount(),
                    SizeBytes = g.Sum(x => x.StoredSizeBytes),
                })
                .FirstOrDefaultAsync(cancellationToken);

            var hourlyAggregates = await gcBaseQuery
                .Where(c => c.GCScheduledAfter >= rangeStartUtc)
                .GroupBy(x => new
                {
                    x.GCScheduledAfter!.Value.Year,
                    x.GCScheduledAfter!.Value.Month,
                    x.GCScheduledAfter!.Value.Day,
                    x.GCScheduledAfter!.Value.Hour,
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Key.Hour,
                    ChunkCount = g.LongCount(),
                    SizeBytes = g.Sum(x => x.StoredSizeBytes),
                })
                .ToListAsync(cancellationToken);

            Dictionary<DateTime, (long ChunkCount, long SizeBytes)> bucketsMap = [];

            if (overdueAggregate is not null && overdueAggregate.ChunkCount > 0)
            {
                hourlyAggregates.Add(new
                {
                    rangeStartUtc.Year,
                    rangeStartUtc.Month,
                    rangeStartUtc.Day,
                    rangeStartUtc.Hour,
                    overdueAggregate.ChunkCount,
                    overdueAggregate.SizeBytes,
                });
            }

            foreach (var item in hourlyAggregates)
            {
                DateTime hourStartUtc = new(item.Year, item.Month, item.Day, item.Hour, 0, 0, DateTimeKind.Utc);
                DateTime local = TimeZoneInfo.ConvertTimeFromUtc(hourStartUtc, effectiveTimeZone);
                DateTime localBucketStart = normalizedBucket == "day"
                    ? new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                    : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
                TimeSpan bucketOffset = effectiveTimeZone.GetUtcOffset(localBucketStart);
                DateTime bucketUtc = new DateTimeOffset(localBucketStart, bucketOffset).UtcDateTime;

                if (!bucketsMap.TryGetValue(bucketUtc, out var existing))
                {
                    bucketsMap[bucketUtc] = (item.ChunkCount, item.SizeBytes);
                }
                else
                {
                    bucketsMap[bucketUtc] = (existing.ChunkCount + item.ChunkCount, existing.SizeBytes + item.SizeBytes);
                }
            }

            List<GcChunkTimelineBucketDto> buckets = [.. bucketsMap
                .OrderBy(x => x.Key)
                .Select(x => new GcChunkTimelineBucketDto
                {
                    BucketStartUtc = x.Key,
                    ChunkCount = x.Value.ChunkCount,
                    SizeBytes = x.Value.SizeBytes,
                })];

            long totalChunks = buckets.Sum(x => x.ChunkCount);
            long totalSizeBytes = buckets.Sum(x => x.SizeBytes);
            var storageStats = await GetStorageUsageStatsAsync(now, cancellationToken);

            return new GcChunkTimelineDto
            {
                Bucket = normalizedBucket,
                From = rangeStartUtc,
                To = rangeEndUtc,
                GeneratedAt = now,
                TotalChunks = totalChunks,
                TotalSizeBytes = totalSizeBytes,
                Buckets = buckets,
                Storage = storageStats,
            };
        }

        private async Task<StorageUsageStatsDto> GetStorageUsageStatsAsync(
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            var chunks = _dbContext.Chunks.AsNoTracking();

            long totalUniqueChunkCount = await chunks.LongCountAsync(cancellationToken);
            long totalUniqueChunkPlainSizeBytes = await chunks.SumAsync(x => (long?)x.PlainSizeBytes, cancellationToken) ?? 0L;
            long totalUniqueChunkStoredSizeBytes = await chunks.SumAsync(x => (long?)x.StoredSizeBytes, cancellationToken) ?? 0L;

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
                select (long?)c.PlainSizeBytes)
                .SumAsync(cancellationToken) ?? 0L;

            long referencedUniqueChunkStoredSizeBytes = await (
                from c in chunks
                join r in referenced on c.Hash equals r.ChunkHash
                select (long?)c.StoredSizeBytes)
                .SumAsync(cancellationToken) ?? 0L;

            long referencedLogicalPlainSizeBytes = await (
                from c in chunks
                join r in referenced on c.Hash equals r.ChunkHash
                select (long?)(c.PlainSizeBytes * r.RefCount))
                .SumAsync(cancellationToken) ?? 0L;

            var pendingGc = chunks
                .Where(c => c.GCScheduledAfter != null
                    && !c.FileManifestChunks.Any()
                    && !_dbContext.FileManifests.Any(fm => fm.SmallFilePreviewHash == c.Hash)
                    && !_dbContext.FileManifests.Any(fm => fm.LargeFilePreviewHash == c.Hash));

            long pendingGcChunkCount = await pendingGc.LongCountAsync(cancellationToken);
            long pendingGcStoredSizeBytes = await pendingGc.SumAsync(x => (long?)x.StoredSizeBytes, cancellationToken) ?? 0L;

            long overdueGcChunkCount = await pendingGc
                .Where(c => c.GCScheduledAfter <= nowUtc)
                .LongCountAsync(cancellationToken);
            long overdueGcStoredSizeBytes = await pendingGc
                .Where(c => c.GCScheduledAfter <= nowUtc)
                .SumAsync(x => (long?)x.StoredSizeBytes, cancellationToken) ?? 0L;

            long dedupSavedBytes = Math.Max(0, referencedLogicalPlainSizeBytes - referencedUniqueChunkPlainSizeBytes);
            long compressionSavedBytes = Math.Max(0, totalUniqueChunkPlainSizeBytes - totalUniqueChunkStoredSizeBytes);

            return new StorageUsageStatsDto
            {
                StorageType = _settings.GetServerSettings().StorageType.ToString(),
                TotalUniqueChunkCount = totalUniqueChunkCount,
                TotalUniqueChunkPlainSizeBytes = totalUniqueChunkPlainSizeBytes,
                TotalUniqueChunkStoredSizeBytes = totalUniqueChunkStoredSizeBytes,
                ReferencedUniqueChunkCount = referencedUniqueChunkCount,
                ReferencedUniqueChunkPlainSizeBytes = referencedUniqueChunkPlainSizeBytes,
                ReferencedUniqueChunkStoredSizeBytes = referencedUniqueChunkStoredSizeBytes,
                ReferencedLogicalChunkCount = referencedLogicalChunkCount,
                ReferencedLogicalPlainSizeBytes = referencedLogicalPlainSizeBytes,
                DeduplicatedUniqueChunkCount = deduplicatedUniqueChunkCount,
                DedupSavedBytes = dedupSavedBytes,
                CompressionSavedBytes = compressionSavedBytes,
                PendingGcChunkCount = pendingGcChunkCount,
                PendingGcStoredSizeBytes = pendingGcStoredSizeBytes,
                OverdueGcChunkCount = overdueGcChunkCount,
                OverdueGcStoredSizeBytes = overdueGcStoredSizeBytes,
            };
        }

        private static TimeZoneInfo ResolveTimelineTimeZone(string? timezoneId)
        {
            if (!string.IsNullOrWhiteSpace(timezoneId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(timezoneId.Trim(), out TimeZoneInfo? headerTimeZone))
            {
                return headerTimeZone;
            }

            return TimeZoneInfo.Local;
        }
    }
}
