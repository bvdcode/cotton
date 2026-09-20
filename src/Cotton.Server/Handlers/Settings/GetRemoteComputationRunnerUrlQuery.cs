// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Providers;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;

namespace Cotton.Server.Handlers.Settings
{
    public class GetRemoteComputationRunnerUrlQuery : IRequest<string?>
    {
    }

    public class GetRemoteComputationRunnerUrlQueryHandler(SettingsProvider _settings)
        : IRequestHandler<GetRemoteComputationRunnerUrlQuery, string?>
    {
        public Task<string?> Handle(
            GetRemoteComputationRunnerUrlQuery request,
            CancellationToken cancellationToken)
        {
            string? url = _settings.GetServerSettings().RemoteComputationRunnerUrl;
            return Task.FromResult(url);
        }
    }
}
