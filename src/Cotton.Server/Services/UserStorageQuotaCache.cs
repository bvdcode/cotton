// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Coordinates process-wide cached storage usage for quota checks.
    /// </summary>
    public class UserStorageQuotaCache(IMemoryCache _cache)
    {
        private static readonly TimeSpan EntryDuration = TimeSpan.FromMinutes(15);
        private readonly Lock _cacheLock = new();

        internal async Task<long> GetOrLoadAsync(
            Guid userId,
            Func<CancellationToken, Task<long>> loader,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(loader);

            if (TryGet(userId, out long cachedUsedBytes))
            {
                return cachedUsedBytes;
            }

            long loadedUsedBytes = Math.Max(0, await loader(cancellationToken));
            lock (_cacheLock)
            {
                if (TryGetCore(userId, out cachedUsedBytes))
                {
                    return cachedUsedBytes;
                }

                SetCore(userId, loadedUsedBytes);
                return loadedUsedBytes;
            }
        }

        internal void Set(Guid userId, long usedBytes)
        {
            lock (_cacheLock)
            {
                SetCore(userId, Math.Max(0, usedBytes));
            }
        }

        internal void AddIfCached(Guid userId, long bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);

            lock (_cacheLock)
            {
                if (!TryGetCore(userId, out long usedBytes))
                {
                    return;
                }

                long adjustedUsedBytes = usedBytes > long.MaxValue - bytes
                    ? long.MaxValue
                    : usedBytes + bytes;
                SetCore(userId, adjustedUsedBytes);
            }
        }

        internal void RemoveIfCached(Guid userId, long bytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(bytes);

            lock (_cacheLock)
            {
                if (!TryGetCore(userId, out long usedBytes))
                {
                    return;
                }

                SetCore(userId, Math.Max(0, usedBytes - bytes));
            }
        }

        private bool TryGet(Guid userId, out long usedBytes)
        {
            lock (_cacheLock)
            {
                return TryGetCore(userId, out usedBytes);
            }
        }

        private bool TryGetCore(Guid userId, out long usedBytes)
        {
            return _cache.TryGetValue(GetCacheKey(userId), out usedBytes);
        }

        private void SetCore(Guid userId, long usedBytes)
        {
            _cache.Set(GetCacheKey(userId), usedBytes, EntryDuration);
        }

        private static string GetCacheKey(Guid userId) => $"user-storage-quota:used:{userId:N}";
    }
}
