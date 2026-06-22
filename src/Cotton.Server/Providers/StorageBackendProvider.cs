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
            return TryGet(out var type)
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

    /// <summary>
    /// Provides storage backend dependencies to server components.
    /// </summary>
    public class StorageBackendProvider(
        IStorageBackendTypeCache _storageTypeCache,
        SettingsProvider _settings,
        IServiceProvider _serviceProvider) : global::Cotton.Storage.Abstractions.IStorageBackendProvider
    {
        /// <summary>
        /// Gets backend.
        /// </summary>
        public global::Cotton.Storage.Abstractions.IStorageBackend GetBackend()
        {
            if (!_storageTypeCache.TryGet(out var type))
            {
                type = _settings.GetServerSettings().StorageType;
                _storageTypeCache.Set(type);
            }
            if (type == StorageType.S3)
            {
                return ActivatorUtilities.CreateInstance<global::Cotton.Storage.Backends.S3StorageBackend>(_serviceProvider);
            }
            if (type == StorageType.Local)
            {
                return ActivatorUtilities.CreateInstance<global::Cotton.Storage.Backends.FileSystemStorageBackend>(_serviceProvider);
            }
            throw new NotSupportedException($"Storage type {type} is not supported.");
        }
    }
}
