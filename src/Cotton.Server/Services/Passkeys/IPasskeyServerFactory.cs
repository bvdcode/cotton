// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Passkeys;

namespace Cotton.Server.Services.Passkeys
{
    public interface IPasskeyServerFactory
    {
        Task<IPasskeyServer> CreateAsync(CancellationToken cancellationToken);
    }
}
