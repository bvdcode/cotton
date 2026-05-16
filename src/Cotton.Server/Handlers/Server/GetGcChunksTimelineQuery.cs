using Cotton.Database;
using Cotton.Server.Models.Dto;
using Cotton.Server.Providers;
using Cotton.Server.Services;
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
        SettingsProvider _settings,
        ChunkUsageService _chunkUsage) : IRequestHandler<GetGcChunksTimelineQuery, GcChunkTimelineDto>
    {
        private const int DefaultGcTimelineHorizonDays = 30;
        private const int MaxGcTimelineHorizonDays = 365;

        public async Task<GcChunkTimelineDto> Handle(GetGcChunksTimelineQuery request, CancellationToken cancellationToken)
        {
            string normalizedBucket = NormalizeBucketOrThrow(request.Bucket);
            TimeZoneInfo effectiveTimeZone = ResolveTimelineTimeZone(request.TimezoneId);

            DateTime now = DateTime.UtcNow;
            (DateTime rangeStartUtc, DateTime rangeEndUtc) = ResolveRangeOrThrow(request.FromUtc, request.ToUtc, now);

            HashSet<string> protectedStorageKeys = await _chunkUsage.GetProtectedStorageKeysAsync(cancellationToken);
            var hourlyAggregates = await LoadHourlyAggregatesAsync(rangeStartUtc, rangeEndUtc, protectedStorageKeys, cancellationToken);

            var bucketsMap = AggregateIntoBuckets(hourlyAggregates, normalizedBucket, effectiveTimeZone);
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
            var storageStats = await GetStorageUsageStatsAsync(now, protectedStorageKeys, cancellationToken);

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

        private static string NormalizeBucketOrThrow(string bucket)
        {
            string normalizedBucket = bucket.Trim().ToLowerInvariant();
            if (normalizedBucket is not ("hour" or "day"))
            {
                throw new BadRequestException("Invalid bucket value. Supported values: 'hour', 'day'.");
            }
            return normalizedBucket;
        }

        private static (DateTime Start, DateTime End) ResolveRangeOrThrow(DateTime? fromUtc, DateTime? toUtc, DateTime now)
        {
            DateTime rangeStartUtc = (fromUtc ?? now).ToUniversalTime();
            DateTime rangeEndUtc = (toUtc ?? rangeStartUtc.AddDays(DefaultGcTimelineHorizonDays)).ToUniversalTime();

            if (rangeEndUtc <= rangeStartUtc)
            {
                throw new BadRequestException("toUtc must be greater than fromUtc.");
            }

            if (rangeEndUtc > rangeStartUtc.AddDays(MaxGcTimelineHorizonDays))
            {
                throw new BadRequestException($"Requested range is too large. Maximum is {MaxGcTimelineHorizonDays} days.");
            }

            return (rangeStartUtc, rangeEndUtc);
        }

        private async Task<List<HourlyAggregate>> LoadHourlyAggregatesAsync(DateTime rangeStartUtc, DateTime rangeEndUtc, HashSet<string> protectedStorageKeys, CancellationToken cancellationToken)
        {
            var gcBaseQuery = _chunkUsage
                .WhereNotProtectedByStorageKeys(
                    _chunkUsage.WhereUnreferencedByDatabase(_dbContext.Chunks.AsNoTracking()),
                    protectedStorageKeys)
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter < rangeEndUtc);

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
                .Select(g => new HourlyAggregate(
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Key.Hour,
                    g.LongCount(),
                    g.Sum(x => x.StoredSizeBytes)))
                .ToListAsync(cancellationToken);

            if (overdueAggregate is not null && overdueAggregate.ChunkCount > 0)
            {
                hourlyAggregates.Add(new HourlyAggregate(
                    rangeStartUtc.Year,
                    rangeStartUtc.Month,
                    rangeStartUtc.Day,
                    rangeStartUtc.Hour,
                    overdueAggregate.ChunkCount,
                    overdueAggregate.SizeBytes));
            }

            return hourlyAggregates;
        }

        private static Dictionary<DateTime, (long ChunkCount, long SizeBytes)> AggregateIntoBuckets(List<HourlyAggregate> hourlyAggregates, string normalizedBucket, TimeZoneInfo effectiveTimeZone)
        {
            Dictionary<DateTime, (long ChunkCount, long SizeBytes)> bucketsMap = [];
            foreach (var item in hourlyAggregates)
            {
                DateTime bucketUtc = ResolveBucketStartUtc(item, normalizedBucket, effectiveTimeZone);
                if (!bucketsMap.TryGetValue(bucketUtc, out var existing))
                {
                    bucketsMap[bucketUtc] = (item.ChunkCount, item.SizeBytes);
                }
                else
                {
                    bucketsMap[bucketUtc] = (existing.ChunkCount + item.ChunkCount, existing.SizeBytes + item.SizeBytes);
                }
            }
            return bucketsMap;
        }

        private static DateTime ResolveBucketStartUtc(HourlyAggregate item, string normalizedBucket, TimeZoneInfo effectiveTimeZone)
        {
            DateTime hourStartUtc = new(item.Year, item.Month, item.Day, item.Hour, 0, 0, DateTimeKind.Utc);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(hourStartUtc, effectiveTimeZone);
            DateTime localBucketStart = normalizedBucket == "day"
                ? new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
            TimeSpan bucketOffset = effectiveTimeZone.GetUtcOffset(localBucketStart);
            return new DateTimeOffset(localBucketStart, bucketOffset).UtcDateTime;
        }

        private sealed record HourlyAggregate(int Year, int Month, int Day, int Hour, long ChunkCount, long SizeBytes);

        private async Task<StorageUsageStatsDto> GetStorageUsageStatsAsync(
            DateTime nowUtc,
            IReadOnlyCollection<string> protectedStorageKeys,
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

            var pendingGc = _chunkUsage
                .WhereNotProtectedByStorageKeys(
                    _chunkUsage.WhereUnreferencedByDatabase(chunks),
                    protectedStorageKeys)
                .Where(c => c.GCScheduledAfter != null);

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
