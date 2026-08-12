// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
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
        ChunkUsageService _chunkUsage,
        StorageUsageStatsService _storageUsageStats) : IRequestHandler<GetGcChunksTimelineQuery, GcChunkTimelineDto>
    {
        private const int DefaultGcTimelineHorizonDays = 30;
        private const int MaxGcTimelineHorizonDays = 365;

        public async Task<GcChunkTimelineDto> Handle(GetGcChunksTimelineQuery request, CancellationToken cancellationToken)
        {
            string normalizedBucket = NormalizeBucket(request.Bucket);
            TimeZoneInfo effectiveTimeZone = ResolveTimelineTimeZone(request.TimezoneId, _settings);
            DateTime now = DateTime.UtcNow;
            TimelineRange range = ResolveRange(request, now);

            HashSet<string> protectedStorageKeys = await _chunkUsage.GetProtectedStorageKeysAsync(cancellationToken);
            IQueryable<Chunk> gcBaseQuery = BuildGcBaseQuery(protectedStorageKeys, range.EndUtc);
            List<HourlyGcAggregate> aggregates = await LoadHourlyAggregatesAsync(gcBaseQuery, range.StartUtc, cancellationToken);
            List<GcChunkTimelineBucketDto> buckets = BuildTimelineBuckets(aggregates, normalizedBucket, effectiveTimeZone);
            StorageUsageStatsDto storageStats = await _storageUsageStats.GetAsync(now, protectedStorageKeys, cancellationToken);

            return BuildTimelineDto(normalizedBucket, range, now, buckets, storageStats);
        }

        private static string NormalizeBucket(string bucket)
        {
            string normalizedBucket = bucket.Trim().ToLowerInvariant();
            return normalizedBucket is "hour" or "day"
                ? normalizedBucket
                : throw new BadRequestException("Invalid bucket value. Supported values: 'hour', 'day'.");
        }

        private static TimelineRange ResolveRange(GetGcChunksTimelineQuery request, DateTime nowUtc)
        {
            DateTime rangeStartUtc = (request.FromUtc ?? nowUtc).ToUniversalTime();
            DateTime rangeEndUtc = (request.ToUtc ?? rangeStartUtc.AddDays(DefaultGcTimelineHorizonDays)).ToUniversalTime();
            ValidateRange(rangeStartUtc, rangeEndUtc);
            return new TimelineRange(rangeStartUtc, rangeEndUtc);
        }

        private static void ValidateRange(DateTime rangeStartUtc, DateTime rangeEndUtc)
        {
            if (rangeEndUtc <= rangeStartUtc)
            {
                throw new BadRequestException("toUtc must be greater than fromUtc.");
            }

            if (rangeEndUtc > rangeStartUtc.AddDays(MaxGcTimelineHorizonDays))
            {
                throw new BadRequestException($"Requested range is too large. Maximum is {MaxGcTimelineHorizonDays} days.");
            }
        }

        private IQueryable<Chunk> BuildGcBaseQuery(
            IReadOnlyCollection<string> protectedStorageKeys,
            DateTime rangeEndUtc)
        {
            return _chunkUsage
                .WhereNotProtectedByStorageKeys(
                    _chunkUsage.WhereUnreferencedByDatabase(_dbContext.Chunks.AsNoTracking()),
                    protectedStorageKeys)
                .Where(c => c.GCScheduledAfter != null
                    && c.GCScheduledAfter < rangeEndUtc);
        }

        private static GcChunkTimelineDto BuildTimelineDto(
            string bucket,
            TimelineRange range,
            DateTime generatedAtUtc,
            IReadOnlyCollection<GcChunkTimelineBucketDto> buckets,
            StorageUsageStatsDto storageStats)
        {
            return new GcChunkTimelineDto
            {
                Bucket = bucket,
                From = range.StartUtc,
                To = range.EndUtc,
                GeneratedAt = generatedAtUtc,
                TotalChunks = buckets.Sum(x => x.ChunkCount),
                TotalSizeBytes = buckets.Sum(x => x.SizeBytes),
                Buckets = [.. buckets],
                Storage = storageStats,
            };
        }

        private static List<GcChunkTimelineBucketDto> BuildTimelineBuckets(
            IEnumerable<HourlyGcAggregate> hourlyAggregates,
            string bucket,
            TimeZoneInfo timeZone)
        {
            Dictionary<DateTime, (long ChunkCount, long SizeBytes)> bucketsMap = [];
            foreach (HourlyGcAggregate item in hourlyAggregates)
            {
                AddAggregateToBucket(bucketsMap, item, bucket, timeZone);
            }

            return [.. bucketsMap
                .OrderBy(x => x.Key)
                .Select(x => new GcChunkTimelineBucketDto
                {
                    BucketStartUtc = x.Key,
                    ChunkCount = x.Value.ChunkCount,
                    SizeBytes = x.Value.SizeBytes,
                })];
        }

        private static void AddAggregateToBucket(
            IDictionary<DateTime, (long ChunkCount, long SizeBytes)> bucketsMap,
            HourlyGcAggregate item,
            string bucket,
            TimeZoneInfo timeZone)
        {
            DateTime bucketUtc = ResolveBucketStartUtc(item, bucket, timeZone);
            bucketsMap[bucketUtc] = bucketsMap.TryGetValue(bucketUtc, out (long ChunkCount, long SizeBytes) existing)
                ? (existing.ChunkCount + item.ChunkCount, existing.SizeBytes + item.SizeBytes)
                : (item.ChunkCount, item.SizeBytes);
        }

        private static DateTime ResolveBucketStartUtc(HourlyGcAggregate item, string bucket, TimeZoneInfo timeZone)
        {
            DateTime hourStartUtc = new(item.Year, item.Month, item.Day, item.Hour, 0, 0, DateTimeKind.Utc);
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(hourStartUtc, timeZone);
            DateTime localBucketStart = bucket == "day"
                ? new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified)
                : new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
            TimeSpan bucketOffset = timeZone.GetUtcOffset(localBucketStart);
            return new DateTimeOffset(localBucketStart, bucketOffset).UtcDateTime;
        }

        private async Task<List<HourlyGcAggregate>> LoadHourlyAggregatesAsync(
            IQueryable<Chunk> gcBaseQuery,
            DateTime rangeStartUtc,
            CancellationToken cancellationToken)
        {
            List<HourlyGcAggregate> hourlyAggregates = await LoadScheduledHourlyAggregatesAsync(gcBaseQuery, rangeStartUtc, cancellationToken);
            HourlyGcAggregate? overdueAggregate = await LoadOverdueAggregateAsync(gcBaseQuery, rangeStartUtc, cancellationToken);
            if (overdueAggregate is not null && overdueAggregate.ChunkCount > 0)
            {
                hourlyAggregates.Add(HourlyGcAggregate.From(rangeStartUtc, overdueAggregate.ChunkCount, overdueAggregate.SizeBytes));
            }

            return hourlyAggregates;
        }

        private static Task<HourlyGcAggregate?> LoadOverdueAggregateAsync(
            IQueryable<Chunk> gcBaseQuery,
            DateTime rangeStartUtc,
            CancellationToken cancellationToken)
        {
            return gcBaseQuery
                .Where(c => c.GCScheduledAfter < rangeStartUtc)
                .GroupBy(_ => 1)
                .Select(g => new HourlyGcAggregate
                {
                    Year = rangeStartUtc.Year,
                    Month = rangeStartUtc.Month,
                    Day = rangeStartUtc.Day,
                    Hour = rangeStartUtc.Hour,
                    ChunkCount = g.LongCount(),
                    SizeBytes = g.Sum(x => x.StoredSizeBytes),
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static Task<List<HourlyGcAggregate>> LoadScheduledHourlyAggregatesAsync(
            IQueryable<Chunk> gcBaseQuery,
            DateTime rangeStartUtc,
            CancellationToken cancellationToken)
        {
            return gcBaseQuery
                .Where(c => c.GCScheduledAfter >= rangeStartUtc)
                .GroupBy(x => new
                {
                    x.GCScheduledAfter!.Value.Year,
                    x.GCScheduledAfter!.Value.Month,
                    x.GCScheduledAfter!.Value.Day,
                    x.GCScheduledAfter!.Value.Hour,
                })
                .Select(g => new HourlyGcAggregate
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Day = g.Key.Day,
                    Hour = g.Key.Hour,
                    ChunkCount = g.LongCount(),
                    SizeBytes = g.Sum(x => x.StoredSizeBytes),
                })
                .ToListAsync(cancellationToken);
        }

        private record TimelineRange(DateTime StartUtc, DateTime EndUtc);

        private class HourlyGcAggregate
        {
            public int Year { get; init; }

            public int Month { get; init; }

            public int Day { get; init; }

            public int Hour { get; init; }

            public long ChunkCount { get; init; }

            public long SizeBytes { get; init; }

            public static HourlyGcAggregate From(DateTime hourStartUtc, long chunkCount, long sizeBytes) => new()
            {
                Year = hourStartUtc.Year,
                Month = hourStartUtc.Month,
                Day = hourStartUtc.Day,
                Hour = hourStartUtc.Hour,
                ChunkCount = chunkCount,
                SizeBytes = sizeBytes,
            };
        }

        private static TimeZoneInfo ResolveTimelineTimeZone(string? timezoneId, SettingsProvider settings)
        {
            if (!string.IsNullOrWhiteSpace(timezoneId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(timezoneId.Trim(), out TimeZoneInfo? headerTimeZone))
            {
                return headerTimeZone;
            }

            return settings.GetServerSettings().GetTimezoneInfo();
        }
    }
}
