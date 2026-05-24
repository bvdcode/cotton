// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Storage.Abstractions;

namespace Cotton.Benchmark.Infrastructure
{
    /// <summary>
    /// Returns a fixed storage backend instance for benchmark pipelines.
    /// </summary>
    internal sealed class StaticStorageBackendProvider(IStorageBackend backend) : IStorageBackendProvider
    {
        public IStorageBackend GetBackend() => backend;
    }
}
