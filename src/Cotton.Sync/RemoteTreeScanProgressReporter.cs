// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Remote;

namespace Cotton.Sync
{
    internal class RemoteTreeScanProgressReporter : IProgress<RemoteTreeScanProgress>
    {
        private readonly SyncRunOptions _options;
        private readonly DateTime _startedAtUtc;

        public RemoteTreeScanProgressReporter(SyncRunOptions options, DateTime startedAtUtc)
        {
            _options = options;
            _startedAtUtc = startedAtUtc;
        }

        public void Report(RemoteTreeScanProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            SyncRunProgressReporter.Report(
                _options,
                SyncRunProgressStage.ScanningRemote,
                value.FilesScanned,
                filesTotal: null,
                value.CurrentPath,
                _startedAtUtc);
        }
    }
}
