// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Hubs;
using EasyExtensions.AspNetCore.Authorization.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Propagates revoked auth sessions to access-token validation and realtime clients.
    /// </summary>
    public sealed class SessionRevocationNotifier(
        SessionAccessTokenRevocationStore _sessionRevocations,
        ITokenProvider _tokens,
        IHubContext<EventHub> _eventHub)
    {
        /// <summary>
        /// Marks each session revoked for access-token validation and asks connected clients to reconnect.
        /// </summary>
        public async Task NotifyRevokedAsync(
            Guid userId,
            IEnumerable<string?> sessionIds,
            CancellationToken cancellationToken)
        {
            var seenSessionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string? sessionId in sessionIds)
            {
                if (string.IsNullOrWhiteSpace(sessionId) || !seenSessionIds.Add(sessionId))
                {
                    continue;
                }

                await NotifyRevokedAsync(userId, sessionId, cancellationToken);
            }
        }

        /// <summary>
        /// Marks one session revoked for access-token validation and asks connected clients to reconnect.
        /// </summary>
        public async Task NotifyRevokedAsync(
            Guid userId,
            string? sessionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            _sessionRevocations.Revoke(userId, sessionId, _tokens.TokenLifetime);
            await _eventHub.Clients
                .Group(EventHub.GetSessionGroupName(userId, sessionId))
                .SendCoreAsync(EventHub.SessionRevokedMethod, Array.Empty<object>(), cancellationToken);
        }
    }
}
