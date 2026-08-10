// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Server.IntegrationTests.Common
{
    internal class StaticStorageBackendProvider(IStorageBackend _backend) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend() => _backend;
    }
}
