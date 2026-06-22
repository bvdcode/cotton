// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Localization;
using Cotton.Database.Models.Enums;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.Configuration;
using Cotton.Storage.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Protects the backing storage from writes that would leave too little free space.
    /// </summary>
    /// <remarks>
    /// Capacity checks are cached briefly because they sit on the chunk upload path. Successful write
    /// reservations subtract from the cached available space immediately, so a burst of chunks cannot all
    /// pass against the same stale disk snapshot.
    /// </remarks>
    public class StoragePressureGuard(
        IStorageBackendProvider _backendProvider,
        CottonDbContext _dbContext,
        INotificationsProvider _notifications,
        IMemoryCache _cache,
        IOptions<StoragePressureOptions> _options,
        ILogger<StoragePressureGuard> _logger)
    {
        private const string CapacityCacheKey = "storage-pressure:capacity";
        private const string NotificationCacheKey = "storage-pressure:notification-sent";
        private static readonly SemaphoreSlim CapacityReservationLock = new(initialCount: 1, maxCount: 1);
        private static readonly SemaphoreSlim NotificationLock = new(initialCount: 1, maxCount: 1);

        /// <summary>
        /// Ensures the backend can accept the incoming write and reserves that capacity for the request.
        /// </summary>
        public async Task EnsureCanAcceptWriteAsync(long incomingBytes, CancellationToken ct = default)
        {
            using StoragePressureReservation reservation = await ReserveWriteAsync(incomingBytes, ct);
            reservation.Commit();
        }

        internal async Task<StoragePressureReservation> ReserveWriteAsync(long incomingBytes, CancellationToken ct = default)
        {
            StoragePressureOptions options = _options.Value;
            if (!options.Enabled)
            {
                return StoragePressureReservation.None;
            }

            long safeIncomingBytes = Math.Max(0, incomingBytes);
            StoragePressureSnapshot? pressure = null;

            await CapacityReservationLock.WaitAsync(ct);
            try
            {
                CapacityCacheEntry entry = GetOrCreateCapacityEntry(options);
                StorageCapacitySnapshot? capacity = entry.Snapshot;
                if (capacity is null || capacity.TotalBytes <= 0)
                {
                    return StoragePressureReservation.None;
                }

                long requiredFreeBytes = options.GetRequiredFreeBytes(capacity.TotalBytes);
                long availableAfterReservations = Math.Max(0, capacity.AvailableBytes - entry.ReservedBytes);
                long projectedAvailableBytes = availableAfterReservations - safeIncomingBytes;
                if (projectedAvailableBytes >= requiredFreeBytes)
                {
                    entry.Reserve(safeIncomingBytes);
                    return safeIncomingBytes > 0
                        ? new StoragePressureReservation(this, safeIncomingBytes)
                        : StoragePressureReservation.None;
                }

                pressure = new StoragePressureSnapshot(
                    capacity with { AvailableBytes = availableAfterReservations },
                    IncomingBytes: safeIncomingBytes,
                    RequiredFreeBytes: requiredFreeBytes,
                    ProjectedAvailableBytes: Math.Max(0, projectedAvailableBytes));
            }
            finally
            {
                CapacityReservationLock.Release();
            }

            StoragePressureSnapshot pressureToReport = pressure ?? throw new InvalidOperationException("Storage pressure state was not computed.");
            await NotifyAdminsOnceAsync(pressureToReport, options, ct);
            throw new StoragePressureException(pressureToReport);
        }

        private CapacityCacheEntry GetOrCreateCapacityEntry(StoragePressureOptions options)
        {
            if (_cache.TryGetValue(CapacityCacheKey, out CapacityCacheEntry? cached) && cached is not null)
            {
                return cached;
            }

            StorageCapacitySnapshot? snapshot = ReadCapacitySnapshot();
            var entry = new CapacityCacheEntry(snapshot);
            _cache.Set(CapacityCacheKey, entry, options.CheckInterval);
            return entry;
        }

        internal void ReleaseReservation(long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            if (safeBytes == 0)
            {
                return;
            }

            CapacityReservationLock.Wait();
            try
            {
                if (_cache.TryGetValue(CapacityCacheKey, out CapacityCacheEntry? cached))
                {
                    cached?.Release(safeBytes);
                }
            }
            finally
            {
                CapacityReservationLock.Release();
            }
        }

        private StorageCapacitySnapshot? ReadCapacitySnapshot()
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            if (backend is not IStorageCapacityReporter reporter)
            {
                return null;
            }

            try
            {
                return reporter.GetCapacitySnapshot();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Failed to read storage capacity from {BackendType}.", backend.GetType().Name);
                return null;
            }
        }

        private async Task NotifyAdminsOnceAsync(
            StoragePressureSnapshot pressure,
            StoragePressureOptions options,
            CancellationToken ct)
        {
            if (_cache.TryGetValue(NotificationCacheKey, out _))
            {
                return;
            }

            await NotificationLock.WaitAsync(ct);
            try
            {
                if (_cache.TryGetValue(NotificationCacheKey, out _))
                {
                    return;
                }

                List<Guid> adminIds = await _dbContext.Users
                    .AsNoTracking()
                    .Where(x => x.Role == UserRole.Admin)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Id)
                    .ToListAsync(ct);

                if (adminIds.Count == 0)
                {
                    _logger.LogWarning(
                        "Storage pressure guard blocked writes, but no admin users exist for notification. Root: {RootPath}, available: {AvailableBytes}, required: {RequiredFreeBytes}.",
                        pressure.Capacity.RootPath,
                        pressure.Capacity.AvailableBytes,
                        pressure.RequiredFreeBytes);
                }

                string availableSpace = FormatBytes(pressure.Capacity.AvailableBytes);
                string requiredReserve = FormatBytes(pressure.RequiredFreeBytes);
                var metadata = new Dictionary<string, string>
                {
                    ["kind"] = "storage-pressure",
                    ["backend"] = pressure.Capacity.Backend,
                    ["rootPath"] = pressure.Capacity.RootPath,
                    ["availableBytes"] = pressure.Capacity.AvailableBytes.ToString(CultureInfo.InvariantCulture),
                    ["totalBytes"] = pressure.Capacity.TotalBytes.ToString(CultureInfo.InvariantCulture),
                    ["requiredFreeBytes"] = pressure.RequiredFreeBytes.ToString(CultureInfo.InvariantCulture),
                    ["incomingBytes"] = pressure.IncomingBytes.ToString(CultureInfo.InvariantCulture),
                    ["availableSpace"] = availableSpace,
                    ["availablePercent"] = pressure.Capacity.AvailablePercent.ToString("F1", CultureInfo.InvariantCulture),
                    ["requiredReserve"] = requiredReserve,
                };
                Dictionary<string, string> templateMetadata = NotificationTemplateMetadata.Create(
                    NotificationTemplateKeys.StoragePressureTitle,
                    NotificationTemplateKeys.StoragePressureContent,
                    metadata);

                foreach (Guid adminId in adminIds)
                {
                    await _notifications.SendNotificationAsync(
                        adminId,
                        NotificationTemplates.StoragePressureTitle,
                        NotificationTemplates.StoragePressureContent(
                            availableSpace,
                            pressure.Capacity.AvailablePercent,
                            requiredReserve,
                            pressure.Capacity.RootPath),
                        NotificationPriority.High,
                        templateMetadata);
                }

                _cache.Set(NotificationCacheKey, DateTimeOffset.UtcNow, options.AdminNotificationCooldown);
            }
            finally
            {
                NotificationLock.Release();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"];
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }
    }

    internal class CapacityCacheEntry(StorageCapacitySnapshot? snapshot)
    {
        /// <summary>
        /// Gets the snapshot.
        /// </summary>
        public StorageCapacitySnapshot? Snapshot { get; } = snapshot;
        /// <summary>
        /// Gets or sets the reserved bytes.
        /// </summary>
        public long ReservedBytes { get; private set; }

        /// <summary>
        /// Reserves bytes against the cached storage-capacity snapshot.
        /// </summary>
        public void Reserve(long bytes)
        {
            long safeBytes = Math.Max(0, bytes);
            ReservedBytes = safeBytes > long.MaxValue - ReservedBytes
                ? long.MaxValue
                : ReservedBytes + safeBytes;
        }

        /// <summary>
        /// Releases previously reserved bytes after a failed or abandoned write.
        /// </summary>
        public void Release(long bytes)
        {
            ReservedBytes = Math.Max(0, ReservedBytes - Math.Max(0, bytes));
        }
    }

    internal class StoragePressureReservation : IDisposable
    {
        /// <summary>
        /// Creates an empty storage pressure reservation.
        /// </summary>
        public static StoragePressureReservation None => new(null, 0);

        private readonly StoragePressureGuard? _owner;
        private readonly long _bytes;
        private bool _committed;
        private bool _disposed;

        public StoragePressureReservation(StoragePressureGuard? owner, long bytes)
        {
            _owner = owner;
            _bytes = Math.Max(0, bytes);
        }

        /// <summary>
        /// Marks a storage pressure reservation as consumed by a successful write.
        /// </summary>
        public void Commit()
        {
            _committed = true;
        }

        /// <summary>
        /// Releases resources held by this instance.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_committed)
            {
                _owner?.ReleaseReservation(_bytes);
            }
        }
    }

    /// <summary>
    /// Represents storage pressure snapshot.
    /// </summary>
    public record StoragePressureSnapshot(
        StorageCapacitySnapshot Capacity,
        long IncomingBytes,
        long RequiredFreeBytes,
        long ProjectedAvailableBytes);

    /// <summary>
    /// Represents storage pressure exception.
    /// </summary>
    public class StoragePressureException(StoragePressureSnapshot pressure)
        : InvalidOperationException(BuildMessage(pressure))
    {
        /// <summary>
        /// Gets the pressure.
        /// </summary>
        public StoragePressureSnapshot Pressure { get; } = pressure;

        private static string BuildMessage(StoragePressureSnapshot pressure)
        {
            return "Storage is running out of free space. "
                + $"Available bytes: {pressure.Capacity.AvailableBytes}, "
                + $"required reserve: {pressure.RequiredFreeBytes}, "
                + $"incoming bytes: {pressure.IncomingBytes}.";
        }
    }
}
