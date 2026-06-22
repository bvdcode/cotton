// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;

namespace Cotton.Server.Providers
{
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
            if (!_storageTypeCache.TryGet(out StorageType type))
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
