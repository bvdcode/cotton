// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.Bridge;
using System.Security.Cryptography;

namespace Cotton.Server.IntegrationTests.Helpers
{
    public class BridgeTestCredentialProvider : IBridgeCredentialProvider
    {
        public BridgeCredential Credential { get; } = new(Guid.NewGuid(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        public int Calls { get; private set; }

        public Task<BridgeCredential> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Credential);
        }
    }
}
