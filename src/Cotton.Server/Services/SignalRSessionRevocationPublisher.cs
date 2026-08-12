// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Cotton.Server.Services
{
    public class SignalRSessionRevocationPublisher(IHubContext<EventHub> _eventHub)
        : ISessionRevocationPublisher
    {
        public Task PublishAsync(Guid userId, string sessionId, CancellationToken cancellationToken)
        {
            return _eventHub.Clients
                .Group(EventHub.GetSessionGroupName(userId, sessionId))
                .SendCoreAsync(EventHub.SessionRevokedMethod, Array.Empty<object>(), cancellationToken);
        }
    }
}
