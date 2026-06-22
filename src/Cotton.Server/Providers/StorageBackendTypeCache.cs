// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Providers
{
    /// <summary>
    /// Caches storage backend type state.
    /// </summary>
    public class StorageBackendTypeCache : IStorageBackendTypeCache
    {
        private int _hasValue;
        private StorageType _value;

        /// <summary>
        /// Gets value.
        /// </summary>
        public StorageType Get()
        {
            return TryGet(out StorageType type)
                ? type
                : throw new InvalidOperationException("Storage backend type cache is not initialized.");
        }

        /// <summary>
        /// Attempts to get value.
        /// </summary>
        public bool TryGet(out StorageType type)
        {
            if (Volatile.Read(ref _hasValue) == 1)
            {
                type = _value;
                return true;
            }

            type = default;
            return false;
        }

        /// <summary>
        /// Sets value.
        /// </summary>
        public void Set(StorageType type)
        {
            _value = type;
            Volatile.Write(ref _hasValue, 1);
        }

        /// <summary>
        /// Clears the cached value so it will be resolved again.
        /// </summary>
        public void Reset()
        {
            Volatile.Write(ref _hasValue, 0);
            _value = default;
        }
    }
}
