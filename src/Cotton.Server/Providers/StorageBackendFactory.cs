// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Backends;

namespace Cotton.Server.Providers
{
    /// <summary>
    /// Creates the configured storage backend.
    /// </summary>
    public class StorageBackendFactory(
        ILogger<FileSystemStorageBackend> _fileSystemLogger,
        ILogger<S3StorageBackend> _s3Logger)
    {
        /// <summary>
        /// Creates a storage backend for the supplied type.
        /// </summary>
        public IStorageBackend Create(
            StorageType storageType,
            IS3Provider? s3Provider = null,
            string? localBasePath = null)
        {
            return storageType switch
            {
                StorageType.Local => new FileSystemStorageBackend(_fileSystemLogger, localBasePath),
                StorageType.S3 => new S3StorageBackend(
                    s3Provider ?? throw new InvalidOperationException("S3 provider is required for S3 storage."),
                    _s3Logger),
                _ => throw new NotSupportedException($"Storage type {storageType} is not supported.")
            };
        }
    }
}
