// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.LocalChanges
{

    /// <summary>
    /// Watches a local sync root through <see cref="FileSystemWatcher" />.
    /// </summary>
    public sealed class FileSystemLocalSyncRootWatcher : ILocalSyncRootWatcher
    {
        private const int InternalBufferSizeBytes = 64 * 1024;
        private readonly Guid _syncPairId;
        private readonly string _localRootPath;
        private readonly LocalSyncRootChangeFilter _changeFilter;
        private readonly ILogger<FileSystemLocalSyncRootWatcher> _logger;
        private FileSystemWatcher? _watcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemLocalSyncRootWatcher" /> class.
        /// </summary>
        public FileSystemLocalSyncRootWatcher(
            Guid syncPairId,
            string localRootPath,
            ILogger<FileSystemLocalSyncRootWatcher>? logger = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localRootPath);
            _syncPairId = syncPairId;
            _localRootPath = localRootPath;
            _changeFilter = new LocalSyncRootChangeFilter(localRootPath);
            _logger = logger ?? NullLogger<FileSystemLocalSyncRootWatcher>.Instance;
        }

        /// <inheritdoc />
        public event EventHandler<LocalSyncRootChange>? Changed;

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_watcher is not null)
            {
                return Task.CompletedTask;
            }

            if (!Directory.Exists(_localRootPath))
            {
                throw new DirectoryNotFoundException($"Local sync root does not exist: {_localRootPath}.");
            }

            _watcher = new FileSystemWatcher(_localRootPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
                InternalBufferSize = InternalBufferSizeBytes,
            };
            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemWatcher? watcher = _watcher;
            if (watcher is null)
            {
                return Task.CompletedTask;
            }

            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreated;
            watcher.Changed -= OnChanged;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
            _watcher = null;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            Publish(e.FullPath, LocalSyncRootChangeKind.Created);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Publish(e.FullPath, LocalSyncRootChangeKind.Changed);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Publish(e.FullPath, LocalSyncRootChangeKind.Deleted);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            PublishRename(e.OldFullPath, e.FullPath);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Exception exception = e.GetException();
            _logger.LogWarning(
                exception,
                "Local sync root watcher failed for {SyncPairId}. A full reconcile will be requested.",
                _syncPairId);
            Publish(_localRootPath, LocalSyncRootChangeKind.Error);
        }

        private void Publish(string fullPath, LocalSyncRootChangeKind kind)
        {
            if (!_changeFilter.ShouldPublish(fullPath))
            {
                _logger.LogDebug(
                    "Ignoring local sync root watcher event for {SyncPairId} at {ChangedPath}.",
                    _syncPairId,
                    fullPath);
                return;
            }

            Changed?.Invoke(this, new LocalSyncRootChange(_syncPairId, fullPath, kind));
        }

        private void PublishRename(string oldFullPath, string newFullPath)
        {
            if (!_changeFilter.ShouldPublishRename(oldFullPath, newFullPath))
            {
                _logger.LogDebug(
                    "Ignoring local sync root rename watcher event for {SyncPairId} from {OldPath} to {ChangedPath}.",
                    _syncPairId,
                    oldFullPath,
                    newFullPath);
                return;
            }

            Changed?.Invoke(this, new LocalSyncRootChange(_syncPairId, newFullPath, LocalSyncRootChangeKind.Renamed));
        }
    }
}
