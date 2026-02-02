// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Caching.Memory;

namespace Cotton.Server.Services.WebDav;

public sealed class WebDavAuthCache(IMemoryCache cache)
{
    private const string UsernameVersionPrefix = "webdav-basic:ver:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public string GetOrBumpUsernameCacheVersion(string username)
    {
        var versionKey = GetUsernameVersionKey(username);
        if (cache.TryGetValue(versionKey, out string? version) && !string.IsNullOrWhiteSpace(version))
        {
            return version;
        }
        return BumpUsernameCacheVersion(username);
    }

    public string BumpUsernameCacheVersion(string username)
    {
        var versionKey = GetUsernameVersionKey(username);
        var version = Guid.NewGuid().ToString("N");
        cache.Set(versionKey, version, CacheTtl);
        return version;
    }

    public string GetCacheKey(string username, string token)
    {
        var version = GetOrBumpUsernameCacheVersion(username);
        return $"webdav-basic:{username}:{version}:{token}";
    }

    private static string GetUsernameVersionKey(string username) => $"{UsernameVersionPrefix}{username}";
}
