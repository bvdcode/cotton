// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Providers
{
    public class StorageBackendTypeCache : IStorageBackendTypeCache
    {
        private readonly Lock _lock = new();

        // Boxed StorageType when resolved, null otherwise. A single reference keeps reads atomic,
        // and resolving under the same lock as Reset makes a stale value impossible to publish.
        private volatile object? _state;

        public StorageType GetOrAdd(Func<StorageType> resolve)
        {
            if (_state is StorageType cached)
            {
                return cached;
            }

            lock (_lock)
            {
                if (_state is StorageType raced)
                {
                    return raced;
                }

                StorageType resolved = resolve();
                _state = resolved;
                return resolved;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _state = null;
            }
        }
    }
}
