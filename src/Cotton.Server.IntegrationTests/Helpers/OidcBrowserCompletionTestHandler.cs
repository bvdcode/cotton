// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Oidc;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class OidcBrowserCompletionTestHandler : IRequestHandler<CompleteOidcAuthenticationRequest, string>
    {
        public List<string> CompletedStates { get; } = [];

        public Task<string> Handle(CompleteOidcAuthenticationRequest request, CancellationToken cancellationToken)
        {
            CompletedStates.Add(request.State);
            return Task.FromResult("/files");
        }
    }
}
