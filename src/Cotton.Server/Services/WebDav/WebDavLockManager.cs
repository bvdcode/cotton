// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;

namespace Cotton.Server.Services.WebDav
{
    public class WebDavLockManager
    {
        private readonly ConcurrentDictionary<string, WebDavLockInfo> _locks = new();
        private static readonly long CleanupIntervalTicks = TimeSpan.FromSeconds(30).Ticks;
        private long _lastCleanupTicks;

        public WebDavLockInfo Create(Guid userId, string path, TimeSpan timeout)
        {
            CleanupExpiredLocks(force: false);
            string normalizedPath = NormalizePath(path);
            WebDavLockInfo lockInfo = new(
                userId,
                normalizedPath,
                $"opaquelocktoken:{Guid.NewGuid():D}",
                DateTimeOffset.UtcNow.Add(timeout));
            _locks[GetKey(userId, normalizedPath)] = lockInfo;
            return lockInfo;
        }

        public void CleanupExpiredLocks()
        {
            CleanupExpiredLocks(force: true);
        }

        public void Unlock(Guid userId, string path, string token)
        {
            string key = GetKey(userId, NormalizePath(path));
            if (_locks.TryGetValue(key, out WebDavLockInfo? lockInfo)
                && string.Equals(lockInfo.Token, token, StringComparison.Ordinal))
            {
                _locks.TryRemove(key, out _);
            }
        }

        public bool IsSatisfied(Guid userId, string path, string? token)
        {
            CleanupExpiredLocks(force: false);
            for (string current = NormalizePath(path); ; current = ParentPath(current))
            {
                if (_locks.TryGetValue(GetKey(userId, current), out WebDavLockInfo? lockInfo))
                {
                    return token is not null
                        && string.Equals(token, lockInfo.Token, StringComparison.Ordinal);
                }

                if (string.IsNullOrEmpty(current))
                {
                    return true;
                }
            }
        }

        private static string NormalizePath(string path)
        {
            return path.Trim(WebDavPathResolver.PathSeparator);
        }

        private static string GetKey(Guid userId, string path)
        {
            return $"{userId:N}:{path}";
        }

        private static string ParentPath(string path)
        {
            int separatorIndex = path.LastIndexOf(WebDavPathResolver.PathSeparator);
            return separatorIndex < 0 ? string.Empty : path[..separatorIndex];
        }

        private void CleanupExpiredLocks(bool force)
        {
            long nowTicks = DateTimeOffset.UtcNow.UtcTicks;
            long lastCleanupTicks = Interlocked.Read(ref _lastCleanupTicks);
            if (!force && nowTicks - lastCleanupTicks < CleanupIntervalTicks)
            {
                return;
            }

            long replacedCleanupTicks = Interlocked.Exchange(ref _lastCleanupTicks, nowTicks);
            if (!force && replacedCleanupTicks != lastCleanupTicks)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach ((string key, WebDavLockInfo lockInfo) in _locks)
            {
                if (lockInfo.ExpiresAt <= now)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }
    }
}
