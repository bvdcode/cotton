// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.WebDav;

public interface IWebDavLockGuard
{
    bool TryAuthorizeWrite(string path, Guid userId, string? ifHeader, out string? tokenUsed);
}

public sealed class WebDavLockGuard(IWebDavLockStore _locks) : IWebDavLockGuard
{
    public bool TryAuthorizeWrite(string path, Guid userId, string? ifHeader, out string? tokenUsed)
    {
        tokenUsed = WebDavLockTokens.TryExtractToken(ifHeader);
        var normalizedPath = NormalizePath(path);

        var l = _locks.GetByPath(normalizedPath);
        if (l is null)
        {
            return true;
        }

        if (l.OwnerId == userId && string.Equals(l.Token, tokenUsed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        var p = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        return "/" + p;
    }
}
