// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Remote
{
    internal class DownloadTransferProgress : IProgress<long>
    {
        private readonly IProgress<SyncTransferProgress> _progress;
        private readonly string _relativePath;
        private readonly long? _totalBytes;

        public DownloadTransferProgress(
            IProgress<SyncTransferProgress> progress,
            string relativePath,
            long? totalBytes)
        {
            _progress = progress;
            _relativePath = relativePath;
            _totalBytes = totalBytes;
        }

        public long LastTransferredBytes { get; private set; }

        public void Report(long transferredBytes)
        {
            LastTransferredBytes = transferredBytes;
            _progress.Report(new SyncTransferProgress(
                SyncTransferDirection.Download,
                _relativePath,
                transferredBytes,
                _totalBytes));
        }
    }
}
