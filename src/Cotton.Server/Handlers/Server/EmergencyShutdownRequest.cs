// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Server
{
    /// <summary>
    /// Represents the emergency shutdown request payload accepted by the API.
    /// </summary>
    public class EmergencyShutdownRequest : IRequest
    {
    }

    /// <summary>
    /// Handles emergency shutdown requests in the mediator pipeline.
    /// </summary>
    public class EmergencyShutdownRequestHandler : IRequestHandler<EmergencyShutdownRequest>
    {
        /// <summary>
        /// Handles the request through the mediator pipeline.
        /// </summary>
        public Task Handle(EmergencyShutdownRequest request, CancellationToken cancellationToken)
        {
            Environment.Exit(1);
            return Task.CompletedTask;
        }
    }
}
