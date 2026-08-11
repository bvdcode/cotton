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
        StorageBackendFactory _backendFactory,
        global::Cotton.Storage.Abstractions.IS3Provider _s3Provider) : global::Cotton.Storage.Abstractions.IStorageBackendProvider
    {
        /// <summary>
        /// Gets backend.
        /// </summary>
        public global::Cotton.Storage.Abstractions.IStorageBackend GetBackend()
        {
            StorageType type = _storageTypeCache.GetOrAdd(() => _settings.GetServerSettings().StorageType);
            return _backendFactory.Create(type, _s3Provider);
        }
    }
}
