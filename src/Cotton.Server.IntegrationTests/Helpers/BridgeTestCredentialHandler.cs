// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Bridge;
using Cotton.Server.Models.Bridge;
using EasyExtensions.Mediator;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class BridgeTestCredentialHandler(BridgeTestCredentialProvider provider)
        : IRequestHandler<GetBridgeCredentialRequest, BridgeCredential>
    {
        public Task<BridgeCredential> Handle(GetBridgeCredentialRequest request, CancellationToken cancellationToken)
        {
            return provider.GetAsync(cancellationToken);
        }
    }
}
