// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.LocalChanges
{
    /// <summary>
    /// Creates filesystem watchers for local sync roots.
    /// </summary>
    public class FileSystemLocalSyncRootWatcherFactory : ILocalSyncRootWatcherFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemLocalSyncRootWatcherFactory" /> class.
        /// </summary>
        public FileSystemLocalSyncRootWatcherFactory(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        /// <inheritdoc />
        public ILocalSyncRootWatcher Create(SyncPairSettings syncPair)
        {
            ArgumentNullException.ThrowIfNull(syncPair);
            return new FileSystemLocalSyncRootWatcher(
                syncPair.Id,
                syncPair.LocalRootPath,
                _loggerFactory.CreateLogger<FileSystemLocalSyncRootWatcher>());
        }
    }
}
