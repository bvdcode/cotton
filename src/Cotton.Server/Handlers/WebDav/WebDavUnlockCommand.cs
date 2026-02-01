// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Server.Services.WebDav;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.WebDav;

public record WebDavUnlockCommand(
    Guid UserId,
    string Path,
    string? LockTokenHeader) : IRequest<WebDavUnlockResult>;

public record WebDavUnlockResult(
    bool Success,
    WebDavUnlockError? Error = null);

public enum WebDavUnlockError
{
    PreconditionFailed
}

public class WebDavUnlockCommandHandler(
    IWebDavLockStore _locks) : IRequestHandler<WebDavUnlockCommand, WebDavUnlockResult>
{
    public Task<WebDavUnlockResult> Handle(WebDavUnlockCommand request, CancellationToken ct)
    {
        var token = WebDavLockTokens.NormalizeToken(request.LockTokenHeader);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(new WebDavUnlockResult(false, WebDavUnlockError.PreconditionFailed));
        }

        return Task.FromResult(
            _locks.TryRelease(token, request.UserId)
                ? new WebDavUnlockResult(true)
                : new WebDavUnlockResult(false, WebDavUnlockError.PreconditionFailed));
    }
}
