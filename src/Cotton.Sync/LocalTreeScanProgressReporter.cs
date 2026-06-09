// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Local;

namespace Cotton.Sync
{
    internal class LocalTreeScanProgressReporter : IProgress<LocalTreeScanProgress>
    {
        private readonly SyncRunOptions _options;
        private readonly DateTime _startedAtUtc;

        public LocalTreeScanProgressReporter(SyncRunOptions options, DateTime startedAtUtc)
        {
            _options = options;
            _startedAtUtc = startedAtUtc;
        }

        public void Report(LocalTreeScanProgress value)
        {
            ArgumentNullException.ThrowIfNull(value);
            SyncRunProgressReporter.Report(
                _options,
                SyncRunProgressStage.ScanningLocal,
                value.FilesScanned,
                filesTotal: null,
                value.CurrentPath,
                _startedAtUtc);
        }
    }
}
