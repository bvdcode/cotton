// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Auth
{
    public record RevokeAuthSessionRequest(Guid UserId, string SessionId) : IRequest;

    public class RevokeAuthSessionRequestHandler(
        RefreshTokenRevocationService revocations,
        SessionRevocationNotifier notifier) : IRequestHandler<RevokeAuthSessionRequest>
    {
        public async Task Handle(RevokeAuthSessionRequest request, CancellationToken cancellationToken)
        {
            RefreshTokenRevocationResult revocation = await revocations.RevokeSessionAsync(
                request.UserId, request.SessionId, DateTime.UtcNow, cancellationToken);
            if (revocation.RevokedTokens > 0)
            {
                await notifier.NotifyRevokedAsync(request.UserId, revocation.SessionIds, cancellationToken);
            }
        }
    }
}
