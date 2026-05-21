// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    public class EmergencyShutdownRequest : IRequest
    {
    }

    public class EmergencyShutdownRequestHandler : IRequestHandler<EmergencyShutdownRequest>
    {
        public Task Handle(EmergencyShutdownRequest request, CancellationToken cancellationToken)
        {
            Environment.Exit(1);
            return Task.CompletedTask;
        }
    }
}
