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
        /// Gets value.
        /// </summary>
        StorageType Get();

        /// <summary>
        /// Sets value.
        /// </summary>
        void Set(StorageType type);

        /// <summary>
        /// Attempts to get value.
        /// </summary>
        bool TryGet(out StorageType type);

        /// <summary>
        /// Clears the cached value.
        /// </summary>
        void Reset();
    }
}
