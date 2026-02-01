// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;

namespace Cotton.Server.Services.WebDav;

public sealed record WebDavLockInfo(
    string Path,
    Guid OwnerId,
    string Token,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public interface IWebDavLockStore
{
    bool TryAcquireExclusive(string path, Guid ownerId, TimeSpan? timeout, out WebDavLockInfo? @lock);
    bool TryRefresh(string token, Guid ownerId, TimeSpan? timeout, out WebDavLockInfo? @lock);
    bool TryRelease(string token, Guid ownerId);
    WebDavLockInfo? GetByPath(string path);
    WebDavLockInfo? GetByToken(string token);
}

public sealed class WebDavInMemoryLockStore : IWebDavLockStore
{
    private readonly ConcurrentDictionary<string, WebDavLockInfo> _locksByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _pathByToken = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAcquireExclusive(string path, Guid ownerId, TimeSpan? timeout, out WebDavLockInfo? @lock)
    {
        CleanupExpired(path);

        var token = $"opaquelocktoken:{Guid.NewGuid():D}";
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = timeout.HasValue ? now.Add(timeout.Value) : null;

        var newLock = new WebDavLockInfo(path, ownerId, token, now, expiresAt);

        if (_locksByPath.TryAdd(path, newLock))
        {
            _pathByToken[token] = path;
            @lock = newLock;
            return true;
        }

        @lock = null;
        return false;
    }

    public bool TryRefresh(string token, Guid ownerId, TimeSpan? timeout, out WebDavLockInfo? @lock)
    {
        if (!_pathByToken.TryGetValue(token, out var path))
        {
            @lock = null;
            return false;
        }

        CleanupExpired(path);

        if (!_locksByPath.TryGetValue(path, out var current) || !string.Equals(current.Token, token, StringComparison.OrdinalIgnoreCase))
        {
            @lock = null;
            return false;
        }

        if (current.OwnerId != ownerId)
        {
            @lock = null;
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = timeout.HasValue ? now.Add(timeout.Value) : current.ExpiresAt;
        var updated = current with { ExpiresAt = expiresAt };

        _locksByPath[path] = updated;
        @lock = updated;
        return true;
    }

    public bool TryRelease(string token, Guid ownerId)
    {
        if (!_pathByToken.TryGetValue(token, out var path))
        {
            return false;
        }

        CleanupExpired(path);

        if (!_locksByPath.TryGetValue(path, out var current) || !string.Equals(current.Token, token, StringComparison.OrdinalIgnoreCase))
        {
            _pathByToken.TryRemove(token, out _);
            return false;
        }

        if (current.OwnerId != ownerId)
        {
            return false;
        }

        _locksByPath.TryRemove(path, out _);
        _pathByToken.TryRemove(token, out _);
        return true;
    }

    public WebDavLockInfo? GetByPath(string path)
    {
        CleanupExpired(path);
        return _locksByPath.TryGetValue(path, out var l) ? l : null;
    }

    public WebDavLockInfo? GetByToken(string token)
    {
        if (!_pathByToken.TryGetValue(token, out var path))
        {
            return null;
        }

        CleanupExpired(path);
        return _locksByPath.TryGetValue(path, out var l) ? l : null;
    }

    private void CleanupExpired(string path)
    {
        if (!_locksByPath.TryGetValue(path, out var current))
        {
            return;
        }

        if (current.ExpiresAt.HasValue && current.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            _locksByPath.TryRemove(path, out _);
            _pathByToken.TryRemove(current.Token, out _);
        }
    }
}
