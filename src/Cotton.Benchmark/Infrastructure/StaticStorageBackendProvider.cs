// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Benchmark.Infrastructure
{
    internal class StaticStorageBackendProvider(IStorageBackend backend) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend() => backend;
    }
}
