// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    /// <summary>
    /// Publishes realtime session revocation events to connected clients.
    /// </summary>
    public interface ISessionRevocationPublisher
    {
        /// <summary>
        /// Publishes one session revocation event.
        /// </summary>
        Task PublishAsync(Guid userId, string sessionId, CancellationToken cancellationToken);
    }
}
