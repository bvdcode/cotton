// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Server.Services
{
    internal class MasterKeyStartupValidator(
        IStorageBackendProvider _backendProvider,
        MasterKeyValidator _validator)
    {
        public Task<MasterKeySentinelResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return _validator.ValidateAsync(
                _backendProvider.GetBackend(),
                cancellationToken: cancellationToken);
        }
    }
}
