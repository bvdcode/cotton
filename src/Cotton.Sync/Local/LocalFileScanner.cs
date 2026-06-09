// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using Cotton.Sync;
using Cotton.Sync.State;

namespace Cotton.Sync.Local
{
    /// <summary>
    /// Scans a local folder and hashes files for synchronization.
    /// </summary>
    public sealed class LocalFileScanner :
        ILocalFileScanner,
        ILocalTreeScanner,
        ILocalFileMetadataTreeScanner,
        ILocalFileMetadataTreeProgressScanner,
        ILocalFileMetadataTreeLookupScanner,
        ILocalFileContentHashProgressHasher
    {
        private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
        private const int ProgressReportItemInterval = 100;
        private const int HashBufferSize = 1024 * 128;
        private static readonly TimeSpan HashProgressReportInterval = TimeSpan.FromMilliseconds(250);
        private static readonly EnumerationOptions ChildEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        /// <inheritdoc />
        public async Task<IReadOnlyList<LocalFileSnapshot>> ScanAsync(
            string rootPath,
            CancellationToken cancellationToken = default)
        {
            LocalTreeSnapshot tree = await ScanTreeAsync(rootPath, cancellationToken).ConfigureAwait(false);
            return tree.Files;
        }

        /// <inheritdoc />
        public async Task<LocalTreeSnapshot> ScanTreeAsync(
            string rootPath,
            CancellationToken cancellationToken = default)
        {
            var tree = new LocalTreeSnapshot();
            await ScanTreeCoreAsync(
                    rootPath,
                    computeHashes: true,
                    progress: null,
                    tree.Directories.Add,
                    tree.Files.Add,
                    cancellationToken)
                .ConfigureAwait(false);
            SortTree(tree);
            return tree;
        }

        /// <inheritdoc />
        public async Task<LocalTreeSnapshot> ScanTreeMetadataAsync(
            string rootPath,
            CancellationToken cancellationToken = default)
        {
            return await ScanTreeMetadataAsync(rootPath, progress: null, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<LocalTreeSnapshot> ScanTreeMetadataAsync(
            string rootPath,
            IProgress<LocalTreeScanProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            var tree = new LocalTreeSnapshot();
            await ScanTreeCoreAsync(
                    rootPath,
                    computeHashes: false,
                    progress,
                    tree.Directories.Add,
                    tree.Files.Add,
                    cancellationToken)
                .ConfigureAwait(false);
            SortTree(tree);
            return tree;
        }

        /// <inheritdoc />
        public async Task<LocalTreeLookupSnapshot> ScanTreeMetadataLookupsAsync(
            string rootPath,
            IProgress<LocalTreeScanProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            var tree = new LocalTreeLookupSnapshot();
            await ScanTreeCoreAsync(
                    rootPath,
                    computeHashes: false,
                    progress,
                    directory => SyncPathLookup.Add(tree.DirectoriesByPath, directory, static item => item.RelativePath),
                    file => SyncPathLookup.Add(tree.FilesByPath, file, static item => item.RelativePath),
                    cancellationToken)
                .ConfigureAwait(false);
            return tree;
        }

        /// <inheritdoc />
        public async Task<string> ComputeContentHashAsync(
            LocalFileSnapshot localFile,
            CancellationToken cancellationToken = default)
        {
            return await ComputeContentHashAsync(localFile, progress: null, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<string> ComputeContentHashAsync(
            LocalFileSnapshot localFile,
            IProgress<SyncTransferProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(localFile);
            ArgumentException.ThrowIfNullOrWhiteSpace(localFile.FullPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(localFile.RelativePath);
            return await ComputeHashAsync(
                    localFile.FullPath,
                    localFile.RelativePath,
                    progress,
                    localFile.SizeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static async Task ScanTreeCoreAsync(
            string rootPath,
            bool computeHashes,
            IProgress<LocalTreeScanProgress>? progress,
            Action<LocalDirectorySnapshot> addDirectory,
            Action<LocalFileSnapshot> addFile,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            ArgumentNullException.ThrowIfNull(addDirectory);
            ArgumentNullException.ThrowIfNull(addFile);
            string fullRoot = Path.GetFullPath(rootPath);
            if (!Directory.Exists(fullRoot))
            {
                throw new DirectoryNotFoundException($"Local sync root was not found: {fullRoot}");
            }

            int directoriesScanned = 0;
            int filesScanned = 0;
            progress?.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
            var pendingDirectories = new Stack<LocalDirectoryScanFrame>();
            pendingDirectories.Push(new LocalDirectoryScanFrame(fullRoot));
            try
            {
                while (pendingDirectories.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    LocalDirectoryScanFrame currentDirectory = pendingDirectories.Peek();
                    if (TryReadNextChildFile(currentDirectory, fullRoot, out FileInfo? file, out string relativePath))
                    {
                        LocalFileSnapshot fileSnapshot = await CreateSnapshotAsync(file, relativePath, computeHashes, cancellationToken)
                            .ConfigureAwait(false);
                        addFile(fileSnapshot);
                        filesScanned++;
                        ReportScanProgress(progress, filesScanned, directoriesScanned, relativePath);
                        continue;
                    }

                    if (TryReadNextChildDirectory(currentDirectory, fullRoot, out LocalDirectorySnapshot? directory))
                    {
                        addDirectory(directory);
                        directoriesScanned++;
                        ReportDirectoryScanProgress(progress, filesScanned, directoriesScanned, directory.RelativePath);
                        pendingDirectories.Push(new LocalDirectoryScanFrame(directory.FullPath));
                        continue;
                    }

                    pendingDirectories.Pop().Dispose();
                }
            }
            finally
            {
                while (pendingDirectories.Count > 0)
                {
                    pendingDirectories.Pop().Dispose();
                }
            }

            progress?.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
        }

        private static bool TryReadNextChildDirectory(
            LocalDirectoryScanFrame currentDirectory,
            string fullRoot,
            out LocalDirectorySnapshot directory)
        {
            while (currentDirectory.TryReadNextDirectoryPath(out string? directoryPath))
            {
                string path = directoryPath ?? throw new InvalidOperationException("Directory enumeration returned a null path.");
                string relativePath = ToRelativePath(fullRoot, path);
                if (LocalFileIgnoreRules.ShouldIgnore(relativePath))
                {
                    continue;
                }

                DirectoryInfo directoryInfo = new(path);
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                directory = new LocalDirectorySnapshot
                {
                    RelativePath = relativePath,
                    FullPath = directoryInfo.FullName,
                };
                return true;
            }

            directory = null!;
            return false;
        }

        private static bool TryReadNextChildFile(
            LocalDirectoryScanFrame currentDirectory,
            string fullRoot,
            out FileInfo file,
            out string relativePath)
        {
            while (currentDirectory.TryReadNextFilePath(out string? filePath))
            {
                string path = filePath ?? throw new InvalidOperationException("File enumeration returned a null path.");
                relativePath = ToRelativePath(fullRoot, path);
                if (LocalFileIgnoreRules.ShouldIgnore(relativePath))
                {
                    continue;
                }

                file = new FileInfo(path);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                return true;
            }

            file = null!;
            relativePath = string.Empty;
            return false;
        }

        private static void SortTree(LocalTreeSnapshot tree)
        {
            tree.Directories.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
            tree.Files.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        }

        private static void ReportScanProgress(
            IProgress<LocalTreeScanProgress>? progress,
            int filesScanned,
            int directoriesScanned,
            string currentPath)
        {
            if (progress is null)
            {
                return;
            }

            if (filesScanned == 1 || filesScanned % ProgressReportItemInterval == 0)
            {
                progress.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath));
            }
        }

        private static void ReportDirectoryScanProgress(
            IProgress<LocalTreeScanProgress>? progress,
            int filesScanned,
            int directoriesScanned,
            string currentPath)
        {
            if (progress is null)
            {
                return;
            }

            if (directoriesScanned == 1 || directoriesScanned % ProgressReportItemInterval == 0)
            {
                progress.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath));
            }
        }

        private static string ToRelativePath(string rootPath, string filePath)
        {
            string relative = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
            return SyncPath.Normalize(relative);
        }

        private static async Task<LocalFileSnapshot> CreateSnapshotAsync(
            FileInfo file,
            string relativePath,
            bool computeHash,
            CancellationToken cancellationToken)
        {
            ValidatePlatformPermissions(file, relativePath);
            LocalFileMetadata before = ReadMetadata(file, relativePath);
            string contentHash = computeHash
                ? await ComputeHashAsync(file.FullName, relativePath, progress: null, before.Length, cancellationToken)
                    .ConfigureAwait(false)
                : string.Empty;
            LocalFileMetadata after = ReadMetadata(file, relativePath);
            if (before.Length != after.Length || before.LastWriteUtc != after.LastWriteUtc)
            {
                throw new LocalFileUnavailableException(relativePath, file.FullName, "the file changed during scanning.");
            }

            return new LocalFileSnapshot
            {
                RelativePath = relativePath,
                FullPath = file.FullName,
                ContentHash = contentHash,
                SizeBytes = after.Length,
                LastWriteUtc = after.LastWriteUtc,
            };
        }

        private static void ValidatePlatformPermissions(FileInfo file, string relativePath)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            UnixFileMode readMask = UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            try
            {
                if ((File.GetUnixFileMode(file.FullName) & readMask) == 0)
                {
                    throw new LocalFilePermissionDeniedException(
                        relativePath,
                        file.FullName,
                        "the file has no Unix read permission bits.");
                }
            }
            catch (LocalFilePermissionDeniedException)
            {
                throw;
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new LocalFilePermissionDeniedException(relativePath, file.FullName, exception);
            }
            catch (IOException exception)
            {
                throw new LocalFileUnavailableException(relativePath, file.FullName, exception);
            }
        }

        private static LocalFileMetadata ReadMetadata(FileInfo file, string relativePath)
        {
            try
            {
                file.Refresh();
                if (!file.Exists)
                {
                    throw new FileNotFoundException("Local file disappeared during scanning.", file.FullName);
                }

                return new LocalFileMetadata(file.Length, file.LastWriteTimeUtc);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new LocalFilePermissionDeniedException(relativePath, file.FullName, exception);
            }
            catch (IOException exception)
            {
                throw new LocalFileUnavailableException(relativePath, file.FullName, exception);
            }
        }

        private static async Task<string> ComputeHashAsync(
            string filePath,
            string relativePath,
            IProgress<SyncTransferProgress>? progress,
            long? totalBytes,
            CancellationToken cancellationToken)
        {
            try
            {
                long bytesRead = 0;
                DateTime lastReportedAtUtc = DateTime.UtcNow;
                ReportHashProgress(progress, relativePath, bytesRead, totalBytes, isCompleted: false);
                await using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: HashBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[HashBufferSize];
                while (true)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    hasher.AppendData(buffer.AsSpan(0, read));
                    bytesRead += read;
                    DateTime now = DateTime.UtcNow;
                    if (now - lastReportedAtUtc >= HashProgressReportInterval)
                    {
                        ReportHashProgress(progress, relativePath, bytesRead, totalBytes, isCompleted: false);
                        lastReportedAtUtc = now;
                    }
                }

                byte[] hash = hasher.GetHashAndReset();
                ReportHashProgress(progress, relativePath, bytesRead, totalBytes, isCompleted: true);
                return Convert.ToHexStringLower(hash);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new LocalFilePermissionDeniedException(relativePath, filePath, exception);
            }
            catch (IOException exception)
            {
                throw new LocalFileUnavailableException(relativePath, filePath, exception);
            }
        }

        private static void ReportHashProgress(
            IProgress<SyncTransferProgress>? progress,
            string relativePath,
            long processedBytes,
            long? totalBytes,
            bool isCompleted)
        {
            if (progress is null)
            {
                return;
            }

            if (totalBytes.HasValue && processedBytes > totalBytes.Value)
            {
                processedBytes = totalBytes.Value;
            }

            progress.Report(new SyncTransferProgress(
                SyncTransferDirection.Hash,
                relativePath,
                processedBytes,
                totalBytes,
                isCompleted));
        }

        private readonly record struct LocalFileMetadata(long Length, DateTime LastWriteUtc);

        private sealed class LocalDirectoryScanFrame : IDisposable
        {
            private readonly IEnumerator<string> _directoryEnumerator;
            private IEnumerator<string>? _fileEnumerator;
            private bool _filesDrained;

            public LocalDirectoryScanFrame(string directoryPath)
            {
                DirectoryPath = directoryPath;
                _directoryEnumerator = Directory
                    .EnumerateDirectories(directoryPath, "*", ChildEnumerationOptions)
                    .GetEnumerator();
            }

            public string DirectoryPath { get; }

            public bool TryReadNextDirectoryPath(out string? directoryPath)
            {
                if (_directoryEnumerator.MoveNext())
                {
                    directoryPath = _directoryEnumerator.Current;
                    return true;
                }

                directoryPath = null;
                return false;
            }

            public bool TryReadNextFilePath(out string? filePath)
            {
                if (_filesDrained)
                {
                    filePath = null;
                    return false;
                }

                _fileEnumerator ??= Directory
                    .EnumerateFiles(DirectoryPath, "*", ChildEnumerationOptions)
                    .GetEnumerator();
                if (_fileEnumerator.MoveNext())
                {
                    filePath = _fileEnumerator.Current;
                    return true;
                }

                _filesDrained = true;
                filePath = null;
                return false;
            }

            public void Dispose()
            {
                _directoryEnumerator.Dispose();
                _fileEnumerator?.Dispose();
            }
        }
    }
}
