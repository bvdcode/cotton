// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace Cotton.Server.Auth
{
    /// <summary>
    /// Tracks failed WebDAV authentication attempts by client address and username.
    /// </summary>
    public class WebDavAuthenticationFailureLimiter(IMemoryCache _cache)
    {
        internal const int FailedAttemptLimit = 10;
        private static readonly TimeSpan FailedAttemptWindow = TimeSpan.FromMinutes(1);
        private readonly Lock _sync = new();

        /// <summary>
        /// Determines whether the partition has reached the failed-attempt limit.
        /// </summary>
        public bool IsLimited(IPAddress clientAddress, string username)
        {
            string key = GetCacheKey(clientAddress, username);
            lock (_sync)
            {
                return _cache.TryGetValue(key, out WebDavAuthenticationFailureCounter? counter)
                    && counter!.Count >= FailedAttemptLimit;
            }
        }

        /// <summary>
        /// Records a failure and reports whether this request exceeded the limit.
        /// </summary>
        public bool RecordFailure(IPAddress clientAddress, string username)
        {
            string key = GetCacheKey(clientAddress, username);
            lock (_sync)
            {
                if (!_cache.TryGetValue(key, out WebDavAuthenticationFailureCounter? counter))
                {
                    counter = new WebDavAuthenticationFailureCounter();
                    _cache.Set(key, counter, FailedAttemptWindow);
                }

                counter!.Count++;
                return counter.Count > FailedAttemptLimit;
            }
        }

        /// <summary>
        /// Clears failures for a successfully authenticated partition.
        /// </summary>
        public void Clear(IPAddress clientAddress, string username)
        {
            string key = GetCacheKey(clientAddress, username);
            lock (_sync)
            {
                _cache.Remove(key);
            }
        }

        private static string GetCacheKey(IPAddress clientAddress, string username)
        {
            ArgumentNullException.ThrowIfNull(clientAddress);
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            return $"webdav-basic-fail:{clientAddress}:{username.Trim().ToLowerInvariant()}";
        }
    }
}
