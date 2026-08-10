// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Jobs
{
    internal static class JobStartupDelays
    {
        private static readonly TimeSpan DumpDatabaseDelay = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan DownloadTokenRetentionDelay = TimeSpan.FromMinutes(4);
        private static readonly TimeSpan StorageConsistencyDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CollectPerformanceDelay = TimeSpan.FromMinutes(6);
        private static readonly TimeSpan ClearTempFolderDelay = TimeSpan.FromMinutes(7);
        private static readonly TimeSpan RefreshTokenRetentionDelay = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan GarbageCollectorDelay = TimeSpan.FromMinutes(15);

        private static int _dumpDatabasePending = 1;
        private static int _downloadTokenRetentionPending = 1;
        private static int _storageConsistencyPending = 1;
        private static int _collectPerformancePending = 1;
        private static int _clearTempFolderPending = 1;
        private static int _refreshTokenRetentionPending = 1;
        private static int _garbageCollectorPending = 1;

        internal static Task WaitForDumpDatabaseAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(DumpDatabaseDelay, ref _dumpDatabasePending, cancellationToken);

        internal static Task WaitForDownloadTokenRetentionAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(DownloadTokenRetentionDelay, ref _downloadTokenRetentionPending, cancellationToken);

        internal static Task WaitForStorageConsistencyAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(StorageConsistencyDelay, ref _storageConsistencyPending, cancellationToken);

        internal static Task WaitForCollectPerformanceAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(CollectPerformanceDelay, ref _collectPerformancePending, cancellationToken);

        internal static Task WaitForClearTempFolderAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(ClearTempFolderDelay, ref _clearTempFolderPending, cancellationToken);

        internal static Task WaitForRefreshTokenRetentionAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(RefreshTokenRetentionDelay, ref _refreshTokenRetentionPending, cancellationToken);

        internal static Task WaitForGarbageCollectorAsync(CancellationToken cancellationToken) =>
            WaitOnceAsync(GarbageCollectorDelay, ref _garbageCollectorPending, cancellationToken);

        internal static Task WaitOnceAsync(
            TimeSpan delay,
            ref int pending,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref pending, 0) == 0)
            {
                return Task.CompletedTask;
            }

            return Task.Delay(delay, cancellationToken);
        }
    }
}
