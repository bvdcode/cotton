// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Cotton.Files;
using Cotton.Nodes;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Tests
{
    public class SyncEnginePerformanceSmokeTests
    {
        private const long MiB = 1024L * 1024L;
        private static readonly Guid RemoteRootNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private string _root = string.Empty;
        private string _databasePath = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "cotton-sync-performance", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _databasePath = Path.Combine(_root, ".cotton-sync", "state.sqlite");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public async Task RunOnceAsync_NoOpForOneThousandFilesCompletesWithinSmokeTarget()
        {
            const int fileCount = 1_000;
            TimeSpan smokeTarget = TimeSpan.FromSeconds(20);

            await VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
                "performance-noop-1k",
                fileCount,
                smokeTarget,
                managedHeapDeltaTargetBytes: 128L * MiB);
        }

        [Test]
        public async Task RunOnceAsync_NoOpForThreeThousandFilesCompletesWithinSmokeTarget()
        {
            const int fileCount = 3_000;
            TimeSpan smokeTarget = TimeSpan.FromSeconds(60);

            await VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
                "performance-noop-3k",
                fileCount,
                smokeTarget,
                managedHeapDeltaTargetBytes: 160L * MiB);
        }

        [Test]
        [Explicit("Release-scale smoke; run manually before release or on dedicated Windows performance verification.")]
        public async Task RunOnceAsync_NoOpForTenThousandFilesCompletesWithinManualSmokeTarget()
        {
            const int fileCount = 10_000;
            TimeSpan smokeTarget = TimeSpan.FromMinutes(3);

            await VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
                "performance-noop-10k",
                fileCount,
                smokeTarget,
                managedHeapDeltaTargetBytes: 256L * MiB);
        }

        [Test]
        [Explicit("Release-scale smoke; run manually before release or on dedicated Windows performance verification.")]
        public async Task RunOnceAsync_NoOpForThirtyThousandFilesCompletesWithinManualSmokeTarget()
        {
            const int fileCount = 30_000;
            TimeSpan smokeTarget = TimeSpan.FromMinutes(8);

            await VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
                "performance-noop-30k",
                fileCount,
                smokeTarget,
                managedHeapDeltaTargetBytes: 512L * MiB);
        }

        [Test]
        [Explicit("Release-scale smoke; run manually before release or on dedicated Windows performance verification.")]
        public async Task RunOnceAsync_NoOpForFiftyThousandFilesCompletesWithinManualSmokeTarget()
        {
            const int fileCount = 50_000;
            TimeSpan smokeTarget = TimeSpan.FromMinutes(12);

            await VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
                "performance-noop-50k",
                fileCount,
                smokeTarget,
                managedHeapDeltaTargetBytes: 768L * MiB);
        }

        [Test]
        [Explicit("Release-scale hot-path smoke; run manually before release on Windows.")]
        public async Task RunOnceAsync_ScopedLocalChangeInFiftyThousandFileTreeAvoidsFullTreeScan()
        {
            await VerifyScopedLocalChangeAvoidsFullTreeScanAsync(
                "performance-scoped-change-50k",
                fileCount: 50_000,
                smokeTarget: TimeSpan.FromSeconds(5));
        }

        [Test]
        [Explicit("Release-scale hot-path smoke; run manually before release on Windows.")]
        public async Task RunOnceAsync_ScopedLocalChangeInOneHundredThousandFileTreeAvoidsFullTreeScan()
        {
            await VerifyScopedLocalChangeAvoidsFullTreeScanAsync(
                "performance-scoped-change-100k",
                fileCount: 100_000,
                smokeTarget: TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task RunOnceAsync_ScopedLocalChangeInOneMillionLogicalEntryStateUsesOnlyScopedLookups()
        {
            const int logicalEntryCount = 1_000_000;
            const string syncPairId = "performance-scoped-change-1m-logical";
            const string changedPath = "Docs/9999/file-999999.txt";
            string oldHash = Hash(Encoding.UTF8.GetBytes("old-content"));
            string newHash = Hash(Encoding.UTF8.GetBytes("new-content"));
            var localScanner = new ScopedPathOnlyLocalScanner(
                changedPath,
                new LocalFileSnapshot
                {
                    RelativePath = changedPath,
                    FullPath = FullPath(changedPath),
                    ContentHash = newHash,
                    SizeBytes = 11,
                    LastWriteUtc = new DateTime(2026, 6, 3, 13, 0, 0, DateTimeKind.Utc),
                });
            NodeFileManifestDto remoteFile = RemoteFile(changedPath, oldHash, sizeBytes: 11);
            var remoteCrawler = new StaticRemoteTreeCrawler(
            [
                new RemoteFileSnapshot
                {
                    RelativePath = changedPath,
                    File = remoteFile,
                },
            ]);
            var remoteFilesClient = new RecordingRemoteFileSynchronizer();
            var stateStore = new CountingScopedStateStore(
                logicalEntryCount,
                new SyncStateEntry
                {
                    SyncPairId = syncPairId,
                    RelativePath = changedPath,
                    Kind = SyncEntryKind.File,
                    LocalContentHash = oldHash,
                    LocalLastWriteUtc = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc),
                    LocalSizeBytes = 11,
                    RemoteNodeId = remoteFile.NodeId,
                    RemoteFileId = remoteFile.Id,
                    RemoteContentHash = remoteFile.ContentHash,
                    RemoteETag = remoteFile.ETag,
                    SyncedAtUtc = new DateTime(2026, 6, 3, 12, 5, 0, DateTimeKind.Utc),
                });
            var engine = new SyncEngine(localScanner, remoteCrawler, remoteFilesClient, stateStore);

            Stopwatch stopwatch = Stopwatch.StartNew();
            remoteFilesClient.MeasurementStopwatch = stopwatch;
            SyncRunResult result = await engine.RunOnceAsync(
                new SyncPair
                {
                    SyncPairId = syncPairId,
                    LocalRootPath = _root,
                    RemoteRootNodeId = RemoteRootNodeId,
                },
                new SyncRunOptions
                {
                    Scope = SyncRunScope.ForLocalChangedPaths([changedPath]),
                });
            stopwatch.Stop();

            TestContext.WriteLine(
                "Scoped local change with {0:N0} logical state entries completed in {1:N0} ms; state GetAsync calls {2}; full state loads {3}; path crawls {4}; full crawls {5}; uploads {6}; first upload started after {7:N0} ms.",
                stateStore.LogicalEntryCount,
                stopwatch.Elapsed.TotalMilliseconds,
                stateStore.GetCalls,
                stateStore.FullLoadCalls,
                remoteCrawler.PathCrawlCalls,
                remoteCrawler.FullCrawlCalls,
                remoteFilesClient.UploadCalls,
                remoteFilesClient.UploadStartedAt.Single().TotalMilliseconds);

            Assert.Multiple(() =>
            {
                Assert.That(localScanner.FullScanCalls, Is.Zero);
                Assert.That(localScanner.PathLookupCalls, Is.EqualTo(1));
                Assert.That(remoteCrawler.PathCrawlCalls, Is.EqualTo(1));
                Assert.That(remoteCrawler.FullCrawlCalls, Is.Zero);
                Assert.That(stateStore.FullLoadCalls, Is.Zero);
                Assert.That(stateStore.GetCalls, Is.EqualTo(3));
                Assert.That(stateStore.UpsertCalls, Is.EqualTo(1));
                Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(1));
                Assert.That(remoteFilesClient.Uploads.Single().RelativePath, Is.EqualTo(changedPath));
                Assert.That(result.Activities.Select(activity => activity.RelativePath), Is.EqualTo(new[] { changedPath }));
                Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
            });
        }

        [Test]
        public async Task RunOnceAsync_UploadsOneThousandSmallFilesWithinSmokeTarget()
        {
            await VerifyInitialUploadFileSetCompletesWithinSmokeTargetAsync(
                "performance-upload-small",
                fileCount: 1_000,
                smokeTarget: TimeSpan.FromSeconds(30),
                managedHeapDeltaTargetBytes: 160L * MiB);
        }

        [Test]
        public async Task RunOnceAsync_UploadsThreeThousandSmallFilesWithinSmokeTarget()
        {
            await VerifyInitialUploadFileSetCompletesWithinSmokeTargetAsync(
                "performance-upload-small-3k",
                fileCount: 3_000,
                smokeTarget: TimeSpan.FromSeconds(90),
                managedHeapDeltaTargetBytes: 256L * MiB);
        }

        [Test]
        [Explicit("Release-scale smoke; run manually before release or on dedicated Windows performance verification.")]
        public async Task RunOnceAsync_UploadsTenThousandSmallFilesWithinManualSmokeTarget()
        {
            await VerifyInitialUploadFileSetCompletesWithinSmokeTargetAsync(
                "performance-upload-small-10k",
                fileCount: 10_000,
                smokeTarget: TimeSpan.FromMinutes(5),
                managedHeapDeltaTargetBytes: 512L * MiB);
        }

        [Test]
        [Explicit("Release-scale smoke; run manually before release or on dedicated Windows performance verification.")]
        public async Task RunOnceAsync_UploadsThirtyThousandSmallFilesWithinManualSmokeTarget()
        {
            await VerifyInitialUploadFileSetCompletesWithinSmokeTargetAsync(
                "performance-upload-small-30k",
                fileCount: 30_000,
                smokeTarget: TimeSpan.FromMinutes(12),
                managedHeapDeltaTargetBytes: 1_024L * MiB);
        }

        [Test]
        public async Task RunOnceAsync_UploadsOneLargeFileWithinSmokeTarget()
        {
            const int fileSizeBytes = 8 * 1024 * 1024;
            const string relativePath = "Large/single-large.bin";
            TimeSpan smokeTarget = TimeSpan.FromSeconds(15);
            byte[] content = CreateDeterministicBytes(fileSizeBytes);
            string expectedHash = Hash(content);
            WriteFile(relativePath, content);
            SqliteSyncStateStore stateStore = new(_databasePath);
            var remoteFilesClient = new RecordingRemoteFileSynchronizer();
            var runProgress = new RecordingProgress<SyncRunProgress>();
            var engine = new SyncEngine(
                new LocalFileScanner(),
                new StaticRemoteTreeCrawler([]),
                remoteFilesClient,
                stateStore);

            Stopwatch stopwatch = Stopwatch.StartNew();
            remoteFilesClient.MeasurementStopwatch = stopwatch;
            SyncRunResult result = await engine.RunOnceAsync(
                new SyncPair
                {
                    SyncPairId = "performance-upload-large",
                    LocalRootPath = _root,
                    RemoteRootNodeId = RemoteRootNodeId,
                },
                new SyncRunOptions { RunProgress = runProgress });
            stopwatch.Stop();

            SyncStateEntry? baseline = await stateStore.GetAsync("performance-upload-large", relativePath);
            TimeSpan localScanElapsed = CalculateStageElapsed(
                runProgress.Values,
                SyncRunProgressStage.ScanningLocal,
                SyncRunProgressStage.ScanningRemote);
            TestContext.WriteLine(
                "Initial upload smoke for one {0:N0}-byte file completed in {1:N0} ms; local metadata scan {2:N0} ms; first upload started after {3:N0} ms.",
                fileSizeBytes,
                stopwatch.Elapsed.TotalMilliseconds,
                localScanElapsed.TotalMilliseconds,
                remoteFilesClient.UploadStartedAt.Single().TotalMilliseconds);

            Assert.Multiple(() =>
            {
                Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(1));
                Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
                Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
                Assert.That(remoteFilesClient.Uploads.Single().RelativePath, Is.EqualTo(relativePath));
                Assert.That(remoteFilesClient.UploadInputContentHashes.Single(), Is.Empty);
                Assert.That(remoteFilesClient.Uploads.Single().LocalFile.SizeBytes, Is.EqualTo(fileSizeBytes));
                Assert.That(remoteFilesClient.Uploads.Single().LocalFile.ContentHash, Is.EqualTo(expectedHash));
                Assert.That(localScanElapsed, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
                Assert.That(result.Activities.Select(activity => activity.Kind), Is.EqualTo(new[] { SyncActivityKind.Uploaded }));
                Assert.That(baseline, Is.Not.Null);
                Assert.That(baseline!.LocalContentHash, Is.EqualTo(expectedHash));
                Assert.That(baseline.RemoteContentHash, Is.EqualTo(expectedHash));
                Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
            });
        }

        private string FullPath(string relativePath)
        {
            return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private void WriteFile(string relativePath, byte[] content)
        {
            string fullPath = FullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
            File.SetLastWriteTimeUtc(fullPath, new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
        }

        private async Task VerifyNoOpFileSetCompletesWithinSmokeTargetAsync(
            string syncPairId,
            int fileCount,
            TimeSpan smokeTarget,
            long managedHeapDeltaTargetBytes)
        {
            SqliteSyncStateStore stateStore = new(_databasePath);
            await stateStore.InitializeAsync();
            List<RemoteFileSnapshot> remoteFiles = [];
            List<SyncStateEntry> baselineEntries = [];

            for (int index = 0; index < fileCount; index++)
            {
                string relativePath = $"Docs/{index / 100:D2}/file-{index:D5}.txt";
                byte[] content = Encoding.UTF8.GetBytes("content-" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
                string hash = Hash(content);
                WriteFile(relativePath, content);
                NodeFileManifestDto remoteFile = RemoteFile(relativePath, hash, content.Length);
                remoteFiles.Add(new RemoteFileSnapshot
                {
                    RelativePath = relativePath,
                    File = remoteFile,
                });
                baselineEntries.Add(new SyncStateEntry
                {
                    SyncPairId = syncPairId,
                    RelativePath = relativePath,
                    Kind = SyncEntryKind.File,
                    LocalContentHash = hash,
                    LocalLastWriteUtc = File.GetLastWriteTimeUtc(FullPath(relativePath)),
                    LocalSizeBytes = content.Length,
                    RemoteNodeId = remoteFile.NodeId,
                    RemoteFileId = remoteFile.Id,
                    RemoteContentHash = remoteFile.ContentHash,
                    RemoteETag = remoteFile.ETag,
                    SyncedAtUtc = DateTime.UtcNow,
                });
            }

            await stateStore.ReplacePairAsync(syncPairId, baselineEntries);

            var remoteFilesClient = new GuardedRemoteFileSynchronizer();
            var runProgress = new RecordingProgress<SyncRunProgress>();
            var engine = new SyncEngine(
                new LocalFileScanner(),
                new StaticRemoteTreeCrawler(remoteFiles),
                remoteFilesClient,
                stateStore);

            MemorySample beforeRunMemory = CaptureMemorySample();
            Stopwatch stopwatch = Stopwatch.StartNew();
            SyncRunResult result = await engine.RunOnceAsync(new SyncPair
            {
                SyncPairId = syncPairId,
                LocalRootPath = _root,
                RemoteRootNodeId = RemoteRootNodeId,
            }, new SyncRunOptions { RunProgress = runProgress });
            stopwatch.Stop();
            MemorySample afterRunMemory = CaptureMemorySample();

            IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync(syncPairId);
            TimeSpan localScanElapsed = CalculateStageElapsed(
                runProgress.Values,
                SyncRunProgressStage.ScanningLocal,
                SyncRunProgressStage.ScanningRemote);
            TimeSpan remoteScanElapsed = CalculateStageElapsed(
                runProgress.Values,
                SyncRunProgressStage.ScanningRemote,
                SyncRunProgressStage.ReconcilingDirectories);
            TimeSpan directoryReconcileElapsed = CalculateStageElapsed(
                runProgress.Values,
                SyncRunProgressStage.ReconcilingDirectories,
                SyncRunProgressStage.ReconcilingFiles);
            TimeSpan fileReconcileElapsed = CalculateStageElapsed(
                runProgress.Values,
                SyncRunProgressStage.ReconcilingFiles,
                SyncRunProgressStage.Completed);
            TestContext.WriteLine(
                "No-op sync smoke for {0} files completed in {1:N0} ms; local scan {2:N0} ms; remote scan {3:N0} ms; directory reconcile {4:N0} ms; file reconcile {5:N0} ms; managed heap delta {6:N1} MiB; working set delta {7:N1} MiB.",
                fileCount,
                stopwatch.Elapsed.TotalMilliseconds,
                localScanElapsed.TotalMilliseconds,
                remoteScanElapsed.TotalMilliseconds,
                directoryReconcileElapsed.TotalMilliseconds,
                fileReconcileElapsed.TotalMilliseconds,
                ToMiB(afterRunMemory.ManagedHeapBytes - beforeRunMemory.ManagedHeapBytes),
                ToMiB(afterRunMemory.WorkingSetBytes - beforeRunMemory.WorkingSetBytes));

            Assert.Multiple(() =>
            {
                Assert.That(result.Activities, Is.Empty);
                Assert.That(remoteFilesClient.UploadCalls, Is.Zero);
                Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
                Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
                Assert.That(baselines, Has.Count.EqualTo(fileCount));
                Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
                Assert.That(
                    afterRunMemory.ManagedHeapBytes - beforeRunMemory.ManagedHeapBytes,
                    Is.LessThan(managedHeapDeltaTargetBytes));
            });
        }

        private async Task VerifyScopedLocalChangeAvoidsFullTreeScanAsync(
            string syncPairId,
            int fileCount,
            TimeSpan smokeTarget)
        {
            string changedPath = $"Docs/{(fileCount - 1) / 100:D2}/file-{fileCount - 1:D5}.txt";
            SqliteSyncStateStore stateStore = new(_databasePath);
            await stateStore.InitializeAsync();
            List<RemoteFileSnapshot> remoteFiles = [];
            List<SyncStateEntry> baselineEntries = [];

            for (int index = 0; index < fileCount; index++)
            {
                string relativePath = $"Docs/{index / 100:D2}/file-{index:D5}.txt";
                byte[] content = Encoding.UTF8.GetBytes("content-" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
                string hash = Hash(content);
                WriteFile(relativePath, content);
                NodeFileManifestDto remoteFile = RemoteFile(relativePath, hash, content.Length);
                remoteFiles.Add(new RemoteFileSnapshot
                {
                    RelativePath = relativePath,
                    File = remoteFile,
                });
                baselineEntries.Add(new SyncStateEntry
                {
                    SyncPairId = syncPairId,
                    RelativePath = relativePath,
                    Kind = SyncEntryKind.File,
                    LocalContentHash = hash,
                    LocalLastWriteUtc = File.GetLastWriteTimeUtc(FullPath(relativePath)),
                    LocalSizeBytes = content.Length,
                    RemoteNodeId = remoteFile.NodeId,
                    RemoteFileId = remoteFile.Id,
                    RemoteContentHash = remoteFile.ContentHash,
                    RemoteETag = remoteFile.ETag,
                    SyncedAtUtc = DateTime.UtcNow,
                });
            }

            await stateStore.ReplacePairAsync(syncPairId, baselineEntries);
            WriteFile(changedPath, Encoding.UTF8.GetBytes("changed-content"));

            var remoteFilesClient = new RecordingRemoteFileSynchronizer();
            var remoteCrawler = new StaticRemoteTreeCrawler(remoteFiles);
            var runProgress = new RecordingProgress<SyncRunProgress>();
            var engine = new SyncEngine(
                new LocalFileScanner(),
                remoteCrawler,
                remoteFilesClient,
                stateStore);

            Stopwatch stopwatch = Stopwatch.StartNew();
            remoteFilesClient.MeasurementStopwatch = stopwatch;
            SyncRunResult result = await engine.RunOnceAsync(
                new SyncPair
                {
                    SyncPairId = syncPairId,
                    LocalRootPath = _root,
                    RemoteRootNodeId = RemoteRootNodeId,
                },
                new SyncRunOptions
                {
                    Scope = SyncRunScope.ForLocalChangedPaths([changedPath]),
                    RunProgress = runProgress,
                });
            stopwatch.Stop();

            TestContext.WriteLine(
                "Scoped local change in {0} file tree completed in {1:N0} ms; path crawls {2}; full crawls {3}; uploads {4}; first upload started after {5:N0} ms.",
                fileCount,
                stopwatch.Elapsed.TotalMilliseconds,
                remoteCrawler.PathCrawlCalls,
                remoteCrawler.FullCrawlCalls,
                remoteFilesClient.UploadCalls,
                remoteFilesClient.UploadStartedAt.Single().TotalMilliseconds);

            Assert.Multiple(() =>
            {
                Assert.That(remoteCrawler.PathCrawlCalls, Is.EqualTo(1));
                Assert.That(remoteCrawler.FullCrawlCalls, Is.Zero);
                Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(1));
                Assert.That(remoteFilesClient.Uploads.Single().RelativePath, Is.EqualTo(changedPath));
                Assert.That(result.Activities.Select(activity => activity.RelativePath), Is.EqualTo(new[] { changedPath }));
                Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
            });
        }

        private async Task VerifyInitialUploadFileSetCompletesWithinSmokeTargetAsync(
            string syncPairId,
            int fileCount,
            TimeSpan smokeTarget,
            long managedHeapDeltaTargetBytes)
        {
            for (int index = 0; index < fileCount; index++)
            {
                string relativePath = $"Upload/{index / 100:D2}/small-{index:D5}.txt";
                byte[] content = Encoding.UTF8.GetBytes("small-upload-" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
                WriteFile(relativePath, content);
            }

            SqliteSyncStateStore stateStore = new(_databasePath);
            var remoteFilesClient = new RecordingRemoteFileSynchronizer();
            var activityProgress = new RecordingProgress<SyncActivity>();
            const int retainedActivityLimit = 100;
            var engine = new SyncEngine(
                new LocalFileScanner(),
                new StaticRemoteTreeCrawler([]),
                remoteFilesClient,
                stateStore);

            MemorySample beforeRunMemory = CaptureMemorySample();
            Stopwatch stopwatch = Stopwatch.StartNew();
            SyncRunResult result = await engine.RunOnceAsync(
                new SyncPair
                {
                    SyncPairId = syncPairId,
                    LocalRootPath = _root,
                    RemoteRootNodeId = RemoteRootNodeId,
                },
                new SyncRunOptions
                {
                    ActivityProgress = activityProgress,
                    MaximumStoredResultActivities = retainedActivityLimit,
                });
            stopwatch.Stop();
            MemorySample afterRunMemory = CaptureMemorySample();

            IReadOnlyList<SyncStateEntry> baselines = await stateStore.LoadPairAsync(syncPairId);
            int distinctRemoteFileIds = baselines
                .Select(entry => entry.RemoteFileId)
                .Where(id => id.HasValue)
                .Distinct()
                .Count();
            TestContext.WriteLine(
                "Initial upload smoke for {0} small files completed in {1:N0} ms; managed heap delta {2:N1} MiB; working set delta {3:N1} MiB.",
                fileCount,
                stopwatch.Elapsed.TotalMilliseconds,
                ToMiB(afterRunMemory.ManagedHeapBytes - beforeRunMemory.ManagedHeapBytes),
                ToMiB(afterRunMemory.WorkingSetBytes - beforeRunMemory.WorkingSetBytes));

            Assert.Multiple(() =>
            {
                Assert.That(remoteFilesClient.UploadCalls, Is.EqualTo(fileCount));
                Assert.That(remoteFilesClient.DownloadCalls, Is.Zero);
                Assert.That(remoteFilesClient.DeleteCalls, Is.Zero);
                Assert.That(activityProgress.Values, Has.Count.EqualTo(fileCount));
                Assert.That(result.TotalActivityCount, Is.EqualTo(fileCount));
                Assert.That(result.Activities, Has.Count.EqualTo(Math.Min(fileCount, retainedActivityLimit)));
                Assert.That(result.IsActivityListTruncated, Is.EqualTo(fileCount > retainedActivityLimit));
                Assert.That(result.Activities.Select(activity => activity.Kind), Is.All.EqualTo(SyncActivityKind.Uploaded));
                Assert.That(baselines, Has.Count.EqualTo(fileCount));
                Assert.That(distinctRemoteFileIds, Is.EqualTo(fileCount));
                Assert.That(stopwatch.Elapsed, Is.LessThan(smokeTarget));
                Assert.That(
                    afterRunMemory.ManagedHeapBytes - beforeRunMemory.ManagedHeapBytes,
                    Is.LessThan(managedHeapDeltaTargetBytes));
            });
        }

        private static string Hash(byte[] bytes)
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        private static TimeSpan CalculateStageElapsed(
            IReadOnlyList<SyncRunProgress> progress,
            SyncRunProgressStage startStage,
            SyncRunProgressStage nextStage)
        {
            SyncRunProgress start = progress.First(item => item.Stage == startStage);
            SyncRunProgress next = progress.First(item => item.Stage == nextStage);
            TimeSpan elapsed = next.OccurredAtUtc - start.OccurredAtUtc;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }

        private static byte[] CreateDeterministicBytes(int length)
        {
            byte[] bytes = new byte[length];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)((index * 31 + index / 17) % 251);
            }

            return bytes;
        }

        private static MemorySample CaptureMemorySample()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            return new MemorySample(GC.GetTotalMemory(forceFullCollection: false), process.WorkingSet64);
        }

        private static double ToMiB(long bytes)
        {
            return bytes / (double)MiB;
        }

        private static NodeFileManifestDto RemoteFile(string relativePath, string contentHash, long sizeBytes)
        {
            return new NodeFileManifestDto
            {
                Id = Guid.NewGuid(),
                CreatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc),
                NodeId = RemoteRootNodeId,
                FileManifestId = Guid.NewGuid(),
                OriginalNodeFileId = Guid.NewGuid(),
                OwnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = relativePath.Split('/')[^1],
                ContentType = "text/plain",
                SizeBytes = sizeBytes,
                ContentHash = contentHash,
                ETag = "sha256-" + contentHash,
                Metadata = new Dictionary<string, string> { ["relativePath"] = relativePath },
            };
        }

        private class StaticRemoteTreeCrawler : IRemoteTreeCrawler, IRemotePathLookupCrawler
        {
            private readonly IReadOnlyList<RemoteFileSnapshot> _files;

            public StaticRemoteTreeCrawler(IReadOnlyList<RemoteFileSnapshot> files)
            {
                _files = files;
            }

            public int FullCrawlCalls { get; private set; }

            public int PathCrawlCalls { get; private set; }

            public Task<RemoteTreeSnapshot> CrawlAsync(Guid rootNodeId, CancellationToken cancellationToken = default)
            {
                FullCrawlCalls++;
                return Task.FromResult(new RemoteTreeSnapshot
                {
                    RootNode = new NodeDto
                    {
                        Id = rootNodeId,
                        Name = "root",
                    },
                    Files = _files.ToList(),
                });
            }

            public Task<RemoteTreeLookupSnapshot> CrawlPathLookupsAsync(
                Guid rootNodeId,
                IReadOnlyCollection<string> relativePaths,
                IProgress<RemoteTreeScanProgress>? progress,
                CancellationToken cancellationToken = default)
            {
                PathCrawlCalls++;
                var snapshot = new RemoteTreeLookupSnapshot
                {
                    RootNode = new NodeDto
                    {
                        Id = rootNodeId,
                        Name = "root",
                    },
                };
                var requested = new HashSet<string>(relativePaths.Select(SyncPath.ToKey), StringComparer.OrdinalIgnoreCase);
                foreach (RemoteFileSnapshot file in _files)
                {
                    if (requested.Contains(SyncPath.ToKey(file.RelativePath)))
                    {
                        snapshot.FilesByPath[SyncPath.ToKey(file.RelativePath)] = file;
                    }
                }

                return Task.FromResult(snapshot);
            }
        }

        private class ScopedPathOnlyLocalScanner :
            ILocalFileScanner,
            ILocalFileMetadataPathLookupScanner,
            ILocalFileContentHasher
        {
            private readonly string _relativePathKey;
            private readonly LocalFileSnapshot _file;

            public ScopedPathOnlyLocalScanner(string relativePath, LocalFileSnapshot file)
            {
                _relativePathKey = SyncPath.ToKey(relativePath);
                _file = file;
            }

            public int FullScanCalls { get; private set; }

            public int PathLookupCalls { get; private set; }

            public Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(
                string rootPath,
                CancellationToken cancellationToken = default)
            {
                FullScanCalls++;
                throw new InvalidOperationException("1M logical hot-path smoke must not run a full local scan.");
            }

            public Task<LocalTreeLookupSnapshot> ScanPathMetadataLookupsAsync(
                string rootPath,
                IReadOnlyCollection<string> relativePaths,
                IProgress<LocalTreeScanProgress>? progress,
                CancellationToken cancellationToken = default)
            {
                PathLookupCalls++;
                var snapshot = new LocalTreeLookupSnapshot();
                if (relativePaths.Select(SyncPath.ToKey).Contains(_relativePathKey, StringComparer.OrdinalIgnoreCase))
                {
                    snapshot.FilesByPath[_relativePathKey] = _file;
                }

                return Task.FromResult(snapshot);
            }

            public Task<string> ComputeContentHashAsync(
                LocalFileSnapshot localFile,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(localFile.ContentHash);
            }
        }

        private class CountingScopedStateStore : ISyncStateStore
        {
            private readonly Dictionary<string, SyncStateEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

            public CountingScopedStateStore(int logicalEntryCount, SyncStateEntry scopedEntry)
            {
                LogicalEntryCount = logicalEntryCount;
                _entries[SyncPath.ToKey(scopedEntry.RelativePath)] = scopedEntry;
            }

            public int LogicalEntryCount { get; }

            public int GetCalls { get; private set; }

            public int FullLoadCalls { get; private set; }

            public int UpsertCalls { get; private set; }

            public Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<SyncStateEntry>> LoadPairAsync(
                string syncPairId,
                CancellationToken cancellationToken = default)
            {
                FullLoadCalls++;
                throw new InvalidOperationException("1M logical hot-path smoke must not load the full state set.");
            }

            public async IAsyncEnumerable<SyncStateEntry> LoadPairEntriesAsync(
                string syncPairId,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                FullLoadCalls++;
                await Task.CompletedTask;
                yield break;
            }

            public Task<DateTime?> GetPairLastSyncedAtUtcAsync(
                string syncPairId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<DateTime?>(DateTime.UtcNow);
            }

            public Task<SyncChangeCursor> GetChangeCursorAsync(
                string syncPairId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new SyncChangeCursor { SyncPairId = syncPairId });
            }

            public Task<SyncStateEntry?> GetAsync(
                string syncPairId,
                string relativePath,
                CancellationToken cancellationToken = default)
            {
                GetCalls++;
                _entries.TryGetValue(SyncPath.ToKey(relativePath), out SyncStateEntry? entry);
                return Task.FromResult(entry);
            }

            public Task UpsertAsync(SyncStateEntry entry, CancellationToken cancellationToken = default)
            {
                UpsertCalls++;
                _entries[SyncPath.ToKey(entry.RelativePath)] = entry;
                return Task.CompletedTask;
            }

            public Task SaveChangeCursorAsync(SyncChangeCursor cursor, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task DeleteAsync(
                string syncPairId,
                string relativePath,
                CancellationToken cancellationToken = default)
            {
                _entries.Remove(SyncPath.ToKey(relativePath));
                return Task.CompletedTask;
            }

            public Task DeletePairAsync(string syncPairId, CancellationToken cancellationToken = default)
            {
                _entries.Clear();
                return Task.CompletedTask;
            }

            public Task ReplacePairAsync(
                string syncPairId,
                IReadOnlyCollection<SyncStateEntry> entries,
                CancellationToken cancellationToken = default)
            {
                FullLoadCalls++;
                throw new InvalidOperationException("1M logical hot-path smoke must not replace full state.");
            }
        }

        private class GuardedRemoteFileSynchronizer : IRemoteFileSynchronizer
        {
            public int UploadCalls { get; private set; }

            public int DownloadCalls { get; private set; }

            public int DeleteCalls { get; private set; }

            public Task<NodeFileManifestDto> UploadFileAsync(
                Guid rootNodeId,
                string relativePath,
                LocalFileSnapshot localFile,
                NodeFileManifestDto? existingRemoteFile = null,
                CancellationToken cancellationToken = default)
            {
                UploadCalls++;
                throw new InvalidOperationException("No-op performance smoke must not upload files.");
            }

            public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
            {
                DownloadCalls++;
                throw new InvalidOperationException("No-op performance smoke must not download files.");
            }

            public Task DeleteFileAsync(
                Guid nodeFileId,
                bool skipTrash = false,
                string? expectedETag = null,
                CancellationToken cancellationToken = default)
            {
                DeleteCalls++;
                throw new InvalidOperationException("No-op performance smoke must not delete files.");
            }
        }

        private class RecordingRemoteFileSynchronizer : IRemoteFileSynchronizer
        {
            public List<UploadCall> Uploads { get; } = [];

            public List<string> UploadInputContentHashes { get; } = [];

            public List<TimeSpan> UploadStartedAt { get; } = [];

            public Stopwatch? MeasurementStopwatch { get; set; }

            public int UploadCalls { get; private set; }

            public int DownloadCalls { get; private set; }

            public int DeleteCalls { get; private set; }

            public async Task<NodeFileManifestDto> UploadFileAsync(
                Guid rootNodeId,
                string relativePath,
                LocalFileSnapshot localFile,
                NodeFileManifestDto? existingRemoteFile = null,
                CancellationToken cancellationToken = default)
            {
                UploadCalls++;
                UploadInputContentHashes.Add(localFile.ContentHash);
                UploadStartedAt.Add(MeasurementStopwatch?.Elapsed ?? TimeSpan.Zero);
                string contentHash = string.IsNullOrWhiteSpace(localFile.ContentHash)
                    ? await HashFileAsync(localFile.FullPath, cancellationToken).ConfigureAwait(false)
                    : localFile.ContentHash;
                NodeFileManifestDto uploaded = RemoteFile(relativePath, contentHash, localFile.SizeBytes);
                uploaded.Id = existingRemoteFile?.Id ?? uploaded.Id;
                uploaded.NodeId = existingRemoteFile?.NodeId ?? rootNodeId;
                uploaded.UpdatedAt = localFile.LastWriteUtc;
                Uploads.Add(new UploadCall(rootNodeId, relativePath, localFile, existingRemoteFile, uploaded));
                return uploaded;
            }

            private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
            {
                await using FileStream stream = File.OpenRead(path);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return Convert.ToHexStringLower(hash);
            }

            public Task DownloadFileAsync(Guid nodeFileId, Stream destination, CancellationToken cancellationToken = default)
            {
                DownloadCalls++;
                throw new InvalidOperationException("Initial upload performance smoke must not download files.");
            }

            public Task DeleteFileAsync(
                Guid nodeFileId,
                bool skipTrash = false,
                string? expectedETag = null,
                CancellationToken cancellationToken = default)
            {
                DeleteCalls++;
                throw new InvalidOperationException("Initial upload performance smoke must not delete files.");
            }
        }

        private record UploadCall(
            Guid RootNodeId,
            string RelativePath,
            LocalFileSnapshot LocalFile,
            NodeFileManifestDto? ExistingRemoteFile,
            NodeFileManifestDto ReturnedFile);

        private record MemorySample(long ManagedHeapBytes, long WorkingSetBytes);

        private class RecordingProgress<T> : IProgress<T>
        {
            public List<T> Values { get; } = [];

            public void Report(T value)
            {
                Values.Add(value);
            }
        }
    }
}
