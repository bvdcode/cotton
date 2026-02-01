// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav;

public record WebDavLockCommand(
    Guid UserId,
    string Path,
    string? IfHeader,
    TimeSpan? Timeout) : IRequest<WebDavLockResult>;

public record WebDavLockResult(
    bool Success,
    bool Created,
    WebDavLockError? Error = null,
    WebDavLockInfo? Lock = null);

public enum WebDavLockError
{
    NotFound,
    Locked,
    PreconditionFailed
}

public class WebDavLockCommandHandler(
    IWebDavPathResolver _pathResolver,
    IWebDavLockStore _locks) : IRequestHandler<WebDavLockCommand, WebDavLockResult>
{
    public async Task<WebDavLockResult> Handle(WebDavLockCommand request, CancellationToken ct)
    {
        // Refresh existing lock if If header contains lock token.
        var token = WebDavLockTokens.TryExtractToken(request.IfHeader);
        if (!string.IsNullOrWhiteSpace(token))
        {
            if (_locks.TryRefresh(token, request.UserId, request.Timeout, out var refreshed) && refreshed is not null)
            {
                return new WebDavLockResult(true, false, Lock: refreshed);
            }

            return new WebDavLockResult(false, false, WebDavLockError.PreconditionFailed);
        }

        var resolved = await _pathResolver.ResolveMetadataAsync(request.UserId, request.Path, ct);
        if (!resolved.Found)
        {
            return new WebDavLockResult(false, false, WebDavLockError.NotFound);
        }

        if (_locks.TryAcquireExclusive(NormalizePath(request.Path), request.UserId, request.Timeout, out var createdLock) && createdLock is not null)
        {
            return new WebDavLockResult(true, true, Lock: createdLock);
        }

        return new WebDavLockResult(false, false, WebDavLockError.Locked);
    }

    private static string NormalizePath(string path)
    {
        var p = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        return "/" + p;
    }
}
