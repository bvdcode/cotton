// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Abstractions;

namespace Cotton.Server.Services
{
    public class SessionRevocationNotifier(
        SessionAccessTokenRevocationStore _sessionRevocations,
        ITokenProvider _tokens,
        ISessionRevocationPublisher _publisher,
        ILogger<SessionRevocationNotifier> _logger)
    {
        public async Task NotifyRevokedAsync(
            Guid userId,
            IEnumerable<string?> sessionIds,
            CancellationToken cancellationToken)
        {
            string[] normalizedSessionIds = sessionIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (string sessionId in normalizedSessionIds)
            {
                _sessionRevocations.Revoke(userId, sessionId, _tokens.TokenLifetime);
            }

            foreach (string sessionId in normalizedSessionIds)
            {
                try
                {
                    await _publisher.PublishAsync(userId, sessionId, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to publish realtime revocation for user {UserId}, session {SessionId}.",
                        userId,
                        sessionId);
                }
            }
        }

        public async Task NotifyRevokedAsync(
            Guid userId,
            string? sessionId,
            CancellationToken cancellationToken)
        {
            await NotifyRevokedAsync(userId, [sessionId], cancellationToken);
        }
    }
}
