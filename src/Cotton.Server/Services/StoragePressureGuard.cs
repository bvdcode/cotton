// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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

namespace Cotton.Server.Services;

public sealed class StoragePressureGuard(
    IStorageBackendProvider _backendProvider,
    CottonDbContext _dbContext,
    INotificationsProvider _notifications,
    IMemoryCache _cache,
    IOptions<StoragePressureOptions> _options,
    ILogger<StoragePressureGuard> _logger)
{
    private const string CapacityCacheKey = "storage-pressure:capacity";
    private const string NotificationCacheKey = "storage-pressure:notification-sent";
    private static readonly SemaphoreSlim NotificationLock = new(initialCount: 1, maxCount: 1);

    public async Task EnsureCanAcceptWriteAsync(long incomingBytes, CancellationToken ct = default)
    {
        StoragePressureOptions options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        StorageCapacitySnapshot? capacity = GetCachedCapacitySnapshot(options);
        if (capacity is null || capacity.TotalBytes <= 0)
        {
            return;
        }

        long safeIncomingBytes = Math.Max(0, incomingBytes);
        long requiredFreeBytes = options.GetRequiredFreeBytes(capacity.TotalBytes);
        long projectedAvailableBytes = capacity.AvailableBytes - safeIncomingBytes;
        if (projectedAvailableBytes >= requiredFreeBytes)
        {
            return;
        }

        var pressure = new StoragePressureSnapshot(
            capacity,
            IncomingBytes: safeIncomingBytes,
            RequiredFreeBytes: requiredFreeBytes,
            ProjectedAvailableBytes: Math.Max(0, projectedAvailableBytes));

        await NotifyAdminsOnceAsync(pressure, options, ct);
        throw new StoragePressureException(pressure);
    }

    private StorageCapacitySnapshot? GetCachedCapacitySnapshot(StoragePressureOptions options)
    {
        if (_cache.TryGetValue(CapacityCacheKey, out CapacityCacheEntry? cached))
        {
            return cached?.Snapshot;
        }

        StorageCapacitySnapshot? snapshot = ReadCapacitySnapshot();
        _cache.Set(CapacityCacheKey, new CapacityCacheEntry(snapshot), options.CheckInterval);
        return snapshot;
    }

    private StorageCapacitySnapshot? ReadCapacitySnapshot()
    {
        var backend = _backendProvider.GetBackend();
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

            var adminIds = await _dbContext.Users
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

            foreach (Guid adminId in adminIds)
            {
                await _notifications.SendNotificationAsync(
                    adminId,
                    NotificationTemplates.StoragePressureTitle,
                    NotificationTemplates.StoragePressureContent(
                        FormatBytes(pressure.Capacity.AvailableBytes),
                        pressure.Capacity.AvailablePercent,
                        FormatBytes(pressure.RequiredFreeBytes),
                        pressure.Capacity.RootPath),
                    NotificationPriority.High,
                    new Dictionary<string, string>
                    {
                        ["kind"] = "storage-pressure",
                        ["backend"] = pressure.Capacity.Backend,
                        ["rootPath"] = pressure.Capacity.RootPath,
                        ["availableBytes"] = pressure.Capacity.AvailableBytes.ToString(),
                        ["totalBytes"] = pressure.Capacity.TotalBytes.ToString(),
                        ["requiredFreeBytes"] = pressure.RequiredFreeBytes.ToString(),
                        ["incomingBytes"] = pressure.IncomingBytes.ToString(),
                    });
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

internal sealed record CapacityCacheEntry(StorageCapacitySnapshot? Snapshot);

public sealed record StoragePressureSnapshot(
    StorageCapacitySnapshot Capacity,
    long IncomingBytes,
    long RequiredFreeBytes,
    long ProjectedAvailableBytes);

public sealed class StoragePressureException(StoragePressureSnapshot pressure)
    : InvalidOperationException(BuildMessage(pressure))
{
    public StoragePressureSnapshot Pressure { get; } = pressure;

    private static string BuildMessage(StoragePressureSnapshot pressure)
    {
        return "Storage is running out of free space. "
            + $"Available bytes: {pressure.Capacity.AvailableBytes}, "
            + $"required reserve: {pressure.RequiredFreeBytes}, "
            + $"incoming bytes: {pressure.IncomingBytes}.";
    }
}
