// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Auth.Oidc;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class OidcReturnUrlTestHandler(string returnUrl)
        : IRequestHandler<CompleteOidcAuthenticationRequest, string>
    {
        public Task<string> Handle(CompleteOidcAuthenticationRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(returnUrl);
        }
    }
}
