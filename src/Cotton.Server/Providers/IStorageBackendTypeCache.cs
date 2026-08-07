// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Providers
{
    /// <summary>
    /// Defines the storage backend type cache contract used by the server runtime.
    /// </summary>
    public interface IStorageBackendTypeCache
    {
        /// <summary>
        /// Returns the cached storage type, resolving and caching it when absent.
        /// </summary>
        StorageType GetOrAdd(Func<StorageType> resolve);

        /// <summary>
        /// Clears the cached value so it will be resolved again.
        /// </summary>
        void Reset();
    }
}
