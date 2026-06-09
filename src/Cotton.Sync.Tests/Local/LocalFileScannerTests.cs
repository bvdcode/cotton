// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using System.Text;
using Cotton.Sync;
using Cotton.Sync.Local;

namespace Cotton.Sync.Tests.Local
{

    public sealed class LocalFileScannerTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "cotton-local-scan", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
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
        public async Task ScanAsync_ReturnsNestedFilesWithNormalizedPathsAndHashes()
        {
            WriteFile("alpha.txt", "alpha");
            WriteFile(Path.Combine("Docs", "Report.txt"), "report");
            var scanner = new LocalFileScanner();

            IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "alpha.txt", "Docs/Report.txt" }));
                Assert.That(files.Single(x => x.RelativePath == "alpha.txt").ContentHash, Is.EqualTo(Hash("alpha")));
                Assert.That(files.Single(x => x.RelativePath == "Docs/Report.txt").SizeBytes, Is.EqualTo(6));
                Assert.That(files.All(x => x.LastWriteUtc.Kind == DateTimeKind.Utc), Is.True);
            });
        }

        [Test]
        public async Task ScanTreeAsync_ReturnsDirectoriesAndFiles()
        {
            Directory.CreateDirectory(FullPath("empty"));
            Directory.CreateDirectory(FullPath(Path.Combine("nested", "child")));
            WriteFile(Path.Combine("nested", "child", "file.txt"), "content");
            var scanner = new LocalFileScanner();

            LocalTreeSnapshot tree = await scanner.ScanTreeAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(
                    tree.Directories.Select(static directory => directory.RelativePath),
                    Is.EqualTo(new[] { "empty", "nested", "nested/child" }));
                Assert.That(
                    tree.Directories.Single(static directory => directory.RelativePath == "empty").FullPath,
                    Is.EqualTo(FullPath("empty")));
                Assert.That(
                    tree.Files.Select(static file => file.RelativePath),
                    Is.EqualTo(new[] { "nested/child/file.txt" }));
            });
        }

        [Test]
        public async Task ScanTreeMetadataAsync_ReturnsFilesWithoutContentHashes()
        {
            WriteFile("alpha.txt", "alpha");
            var scanner = new LocalFileScanner();

            LocalTreeSnapshot tree = await scanner.ScanTreeMetadataAsync(_root);
            string contentHash = await scanner.ComputeContentHashAsync(tree.Files.Single());

            Assert.Multiple(() =>
            {
                Assert.That(tree.Files.Single().RelativePath, Is.EqualTo("alpha.txt"));
                Assert.That(tree.Files.Single().ContentHash, Is.Empty);
                Assert.That(tree.Files.Single().SizeBytes, Is.EqualTo(5));
                Assert.That(contentHash, Is.EqualTo(Hash("alpha")));
            });
        }

        [Test]
        public async Task ComputeContentHashAsync_ReportsHashProgress()
        {
            WriteFile("alpha.txt", "alpha");
            var scanner = new LocalFileScanner();
            var progress = new RecordingProgress<SyncTransferProgress>();
            LocalTreeSnapshot tree = await scanner.ScanTreeMetadataAsync(_root);

            string contentHash = await scanner.ComputeContentHashAsync(tree.Files.Single(), progress);

            Assert.Multiple(() =>
            {
                Assert.That(contentHash, Is.EqualTo(Hash("alpha")));
                Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(progress.Values.Select(static item => item.Direction), Is.All.EqualTo(SyncTransferDirection.Hash));
                Assert.That(progress.Values[0].TransferredBytes, Is.Zero);
                Assert.That(progress.Values[0].TotalBytes, Is.EqualTo(5));
                Assert.That(progress.Values[^1].TransferredBytes, Is.EqualTo(5));
                Assert.That(progress.Values[^1].IsCompleted, Is.True);
            });
        }

        [Test]
        public async Task ScanTreeMetadataLookupsAsync_ReturnsPathLookupsWithoutContentHashes()
        {
            Directory.CreateDirectory(FullPath("Docs"));
            WriteFile(Path.Combine("Docs", "Report.txt"), "report");
            var scanner = new LocalFileScanner();
            var progress = new RecordingProgress<LocalTreeScanProgress>();

            LocalTreeLookupSnapshot tree = await scanner.ScanTreeMetadataLookupsAsync(_root, progress);

            Assert.Multiple(() =>
            {
                Assert.That(tree.DirectoriesByPath.Keys, Is.EqualTo(new[] { "DOCS" }));
                Assert.That(tree.DirectoriesByPath["DOCS"].RelativePath, Is.EqualTo("Docs"));
                Assert.That(tree.FilesByPath.Keys, Is.EqualTo(new[] { "DOCS/REPORT.TXT" }));
                Assert.That(tree.FilesByPath["DOCS/REPORT.TXT"].RelativePath, Is.EqualTo("Docs/Report.txt"));
                Assert.That(tree.FilesByPath["DOCS/REPORT.TXT"].ContentHash, Is.Empty);
                Assert.That(tree.FilesByPath["DOCS/REPORT.TXT"].SizeBytes, Is.EqualTo(6));
                Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
                Assert.That(progress.Values[^1].FilesScanned, Is.EqualTo(1));
                Assert.That(progress.Values[^1].DirectoriesScanned, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ScanTreeMetadataAsync_ReportsScanProgressAsFilesAreDiscovered()
        {
            WriteFile("alpha.txt", "alpha");
            WriteFile(Path.Combine("Docs", "Report.txt"), "report");
            var scanner = new LocalFileScanner();
            var progress = new RecordingProgress<LocalTreeScanProgress>();

            await scanner.ScanTreeMetadataAsync(_root, progress);

            Assert.Multiple(() =>
            {
                Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
                Assert.That(progress.Values[0].FilesScanned, Is.Zero);
                Assert.That(progress.Values[0].DirectoriesScanned, Is.Zero);
                Assert.That(progress.Values.Any(item => item.FilesScanned == 1 && item.CurrentPath == "alpha.txt"), Is.True);
                Assert.That(progress.Values[^1].FilesScanned, Is.EqualTo(2));
                Assert.That(progress.Values[^1].CurrentPath, Is.Empty);
            });
        }

        [Test]
        public async Task ScanTreeMetadataAsync_ReportsScanProgressAsDirectoriesAreDiscovered()
        {
            Directory.CreateDirectory(FullPath("Docs"));
            Directory.CreateDirectory(FullPath(Path.Combine("Videos", "Clips")));
            var scanner = new LocalFileScanner();
            var progress = new RecordingProgress<LocalTreeScanProgress>();

            await scanner.ScanTreeMetadataAsync(_root, progress);

            Assert.Multiple(() =>
            {
                Assert.That(progress.Values, Has.Count.GreaterThanOrEqualTo(3));
                Assert.That(
                    progress.Values.Any(item =>
                        item.FilesScanned == 0
                        && item.DirectoriesScanned == 1
                        && (item.CurrentPath == "Docs" || item.CurrentPath == "Videos")),
                    Is.True);
                Assert.That(progress.Values[^1].FilesScanned, Is.Zero);
                Assert.That(progress.Values[^1].DirectoriesScanned, Is.EqualTo(3));
                Assert.That(progress.Values[^1].CurrentPath, Is.Empty);
            });
        }

        [Test]
        public async Task ScanAsync_IgnoresTempFilesAndCottonWorkingFolder()
        {
            WriteFile("keep.txt", "keep");
            WriteFile("upload.tmp", "tmp");
            WriteFile("upload.temp", "temp");
            WriteFile("download.partial", "partial");
            WriteFile("download.part", "part");
            WriteFile("chrome.crdownload", "partial");
            WriteFile("browser.download", "download");
            WriteFile(".notes.swp", "vim");
            WriteFile(".notes.swo", "vim");
            WriteFile(".notes.swn", "vim");
            WriteFile("~$office.docx", "office");
            WriteFile(".#emacs-lock", "emacs");
            WriteFile("backup~", "backup");
            WriteFile(".DS_Store", "mac");
            WriteFile("Thumbs.db", "windows");
            WriteFile("desktop.ini", "windows");
            WriteFile(Path.Combine("Nested", "DESKTOP.INI"), "windows");
            WriteFile(Path.Combine(".cotton-sync", "state.tmp"), "state");
            var scanner = new LocalFileScanner();

            IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

            Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
        }

        [Test]
        public async Task ScanTreeAsync_IgnoresTemporaryAndCottonWorkingDirectories()
        {
            Directory.CreateDirectory(FullPath("keep"));
            WriteFile(Path.Combine("keep", "file.txt"), "keep");
            WriteFile(Path.Combine(".cotton-sync", "state.db"), "state");
            WriteFile(Path.Combine("partial.tmp", "ignored.txt"), "ignored");
            WriteFile(Path.Combine("download.partial", "ignored.txt"), "ignored");
            var scanner = new LocalFileScanner();

            LocalTreeSnapshot tree = await scanner.ScanTreeAsync(_root);

            Assert.Multiple(() =>
            {
                Assert.That(
                    tree.Directories.Select(static directory => directory.RelativePath),
                    Is.EqualTo(new[] { "keep" }));
                Assert.That(
                    tree.Files.Select(static file => file.RelativePath),
                    Is.EqualTo(new[] { "keep/file.txt" }));
            });
        }

        [Test]
        public async Task ScanTreeAsync_PrunesIgnoredDirectoriesBeforeEnumeratingChildren()
        {
            string blockedDirectory = FullPath(Path.Combine(".cotton-sync", "blocked"));
            Directory.CreateDirectory(blockedDirectory);
            WriteFile(Path.Combine(".cotton-sync", "blocked", "state.db"), "state");
            WriteFile("keep.txt", "keep");
            UnixFileMode? originalMode = null;
            if (!OperatingSystem.IsWindows())
            {
                originalMode = File.GetUnixFileMode(blockedDirectory);
                File.SetUnixFileMode(blockedDirectory, UnixFileMode.None);
            }

            try
            {
                var scanner = new LocalFileScanner();

                LocalTreeSnapshot tree = await scanner.ScanTreeAsync(_root);

                Assert.Multiple(() =>
                {
                    Assert.That(tree.Directories, Is.Empty);
                    Assert.That(tree.Files.Select(static file => file.RelativePath), Is.EqualTo(new[] { "keep.txt" }));
                });
            }
            finally
            {
                if (!OperatingSystem.IsWindows() && originalMode.HasValue)
                {
                    File.SetUnixFileMode(blockedDirectory, originalMode.Value);
                }
            }
        }

        [Test]
        public async Task ScanAsync_IgnoresSymlinkFilesAndDoesNotTraverseSymlinkDirectories()
        {
            WriteFile("target.txt", "target");
            WriteFile(Path.Combine("real-dir", "inside.txt"), "inside");
            string fileLinkPath = Path.Combine(_root, "target-link.txt");
            string directoryLinkPath = Path.Combine(_root, "real-dir-link");
            TryCreateFileSymlink(fileLinkPath, Path.Combine(_root, "target.txt"));
            TryCreateDirectorySymlink(directoryLinkPath, Path.Combine(_root, "real-dir"));
            var scanner = new LocalFileScanner();

            IReadOnlyList<LocalFileSnapshot> files = await scanner.ScanAsync(_root);

            Assert.That(files.Select(x => x.RelativePath), Is.EqualTo(new[] { "real-dir/inside.txt", "target.txt" }));
        }

        [Test]
        public async Task ScanAsync_ThrowsForLockedFile()
        {
            WriteFile("keep.txt", "keep");
            WriteFile("locked.txt", "locked");
            await using FileStream locked = new(
                FullPath("locked.txt"),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var scanner = new LocalFileScanner();

            LocalFileUnavailableException? exception = Assert.ThrowsAsync<LocalFileUnavailableException>(() => scanner.ScanAsync(_root));

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null);
                Assert.That(exception!.RelativePath, Is.EqualTo("locked.txt"));
                Assert.That(exception.FullPath, Is.EqualTo(FullPath("locked.txt")));
                Assert.That(exception.InnerException, Is.TypeOf<IOException>());
            });
        }

        [Test]
        public async Task ScanAsync_ThrowsForUnreadableUnixFile()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Unix file modes are not available on this platform.");
                return;
            }

            WriteFile("unreadable.txt", "secret");
            string path = FullPath("unreadable.txt");
            UnixFileMode originalMode = File.GetUnixFileMode(path);
            var scanner = new LocalFileScanner();

            try
            {
                File.SetUnixFileMode(path, UnixFileMode.None);

                LocalFilePermissionDeniedException? exception = Assert.ThrowsAsync<LocalFilePermissionDeniedException>(() => scanner.ScanAsync(_root));

                Assert.Multiple(() =>
                {
                    Assert.That(exception, Is.Not.Null);
                    Assert.That(exception!.RelativePath, Is.EqualTo("unreadable.txt"));
                    Assert.That(exception.FullPath, Is.EqualTo(path));
                    Assert.That(exception.Reason, Does.Contain("Unix read permission"));
                });
            }
            finally
            {
                File.SetUnixFileMode(path, originalMode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        [Test]
        public void ScanAsync_RejectsMissingRoot()
        {
            var scanner = new LocalFileScanner();
            string missing = Path.Combine(_root, "missing");

            Assert.ThrowsAsync<DirectoryNotFoundException>(() => scanner.ScanAsync(missing));
        }

        private void WriteFile(string relativePath, string text)
        {
            string path = FullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc));
        }

        private string FullPath(string relativePath)
        {
            return Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void TryCreateFileSymlink(string linkPath, string targetPath)
        {
            try
            {
                File.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Ignore("File symlink creation is unavailable in this test environment: " + ex.Message);
            }
        }

        private static void TryCreateDirectorySymlink(string linkPath, string targetPath)
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Ignore("Directory symlink creation is unavailable in this test environment: " + ex.Message);
            }
        }

        private static string Hash(string text)
        {
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        }

        private sealed class RecordingProgress<T> : IProgress<T>
        {
            public List<T> Values { get; } = [];

            public void Report(T value)
            {
                Values.Add(value);
            }
        }
    }
}
