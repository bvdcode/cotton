// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Oidc;
using Cotton.Server.Models;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.WebUtilities;

namespace Cotton.Server.IntegrationTests.Helpers
{
    internal class OidcAuthorizationTestHandler : IRequestHandler<BeginOidcAuthenticationRequest, OidcAuthorizationResult>
    {
        public List<BeginOidcAuthenticationRequest> Starts { get; } = [];

        public Task<OidcAuthorizationResult> Handle(BeginOidcAuthenticationRequest request, CancellationToken cancellationToken)
        {
            Starts.Add(request);
            string state = OidcProtocol.CreateOpaqueValue();
            return Task.FromResult(new OidcAuthorizationResult(
                QueryHelpers.AddQueryString("https://identity.example.test/authorize", "state", state),
                state,
                DateTime.UtcNow.AddMinutes(10)));
        }
    }
}
