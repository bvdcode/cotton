// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services
{
    public interface ISessionRevocationPublisher
    {
        Task PublishAsync(Guid userId, string sessionId, CancellationToken cancellationToken);
    }
}
