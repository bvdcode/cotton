// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Fido2NetLib;

namespace Cotton.Server.Services.Passkeys
{
    public interface IPasskeyClientFactory
    {
        Task<IFido2> CreateAsync(CancellationToken cancellationToken);
    }
}
