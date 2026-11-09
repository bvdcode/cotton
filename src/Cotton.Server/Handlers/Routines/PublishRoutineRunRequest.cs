// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Hubs;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Cotton.Server.Handlers.Routines
{
    public record PublishRoutineRunRequest(Guid UserId, RoutineRunDto Run) : IRequest;

    public class PublishRoutineRunRequestHandler(IHubContext<EventHub> hub) : IRequestHandler<PublishRoutineRunRequest>
    {
        public async Task Handle(PublishRoutineRunRequest request, CancellationToken ct)
        {
            await hub.Clients.User(request.UserId.ToString()).SendAsync("RoutineProgress", request.Run, ct);
        }
    }
}
