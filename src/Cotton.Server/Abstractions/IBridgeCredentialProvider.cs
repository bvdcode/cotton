// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Models.Bridge;

namespace Cotton.Server.Abstractions
{
    public interface IBridgeCredentialProvider
    {
        Task<BridgeCredential> GetAsync(CancellationToken cancellationToken);
    }
}
