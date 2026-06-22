// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services.WebDav
{
    /// <summary>
    /// Supports WebDAV authentication cache behavior.
    /// </summary>
    public class WebDavAuthCache(IMemoryCache cache)
    {
        private const string UsernameVersionPrefix = "webdav-basic:ver:";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or bump username cache version.
        /// </summary>
        public string GetOrBumpUsernameCacheVersion(string username)
        {
            var versionKey = GetUsernameVersionKey(username);
            if (cache.TryGetValue(versionKey, out string? version) && !string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
            return BumpUsernameCacheVersion(username);
        }

        /// <summary>
        /// Executes bump username cache version.
        /// </summary>
        public string BumpUsernameCacheVersion(string username)
        {
            var versionKey = GetUsernameVersionKey(username);
            var version = Guid.NewGuid().ToString("N");
            cache.Set(versionKey, version, CacheTtl);
            return version;
        }

        /// <summary>
        /// Gets cache key.
        /// </summary>
        public string GetCacheKey(string username, string token)
        {
            var version = GetOrBumpUsernameCacheVersion(username);
            return $"webdav-basic:{username}:{version}:{token}";
        }

        private static string GetUsernameVersionKey(string username) => $"{UsernameVersionPrefix}{username}";
    }
}
