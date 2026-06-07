// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using Cotton.Sync;
using Cotton.Sync.State;

namespace Cotton.Sync.Local;

/// <summary>
/// Scans a local folder and hashes files for synchronization.
/// </summary>
public sealed class LocalFileScanner :
    ILocalFileScanner,
    ILocalTreeScanner,
    ILocalFileMetadataTreeScanner,
    ILocalFileMetadataTreeProgressScanner,
    ILocalFileMetadataTreeLookupScanner,
    ILocalFileContentHasher
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private const int ProgressReportItemInterval = 100;
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
        ArgumentNullException.ThrowIfNull(localFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFile.FullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFile.RelativePath);
        return await ComputeHashAsync(localFile.FullPath, localFile.RelativePath, cancellationToken).ConfigureAwait(false);
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
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(fullRoot);
        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDirectory = pendingDirectories.Pop();
            foreach (string directoryPath in Directory.EnumerateDirectories(currentDirectory, "*", ChildEnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = ToRelativePath(fullRoot, directoryPath);
                if (LocalFileIgnoreRules.ShouldIgnore(relativePath))
                {
                    continue;
                }

                DirectoryInfo directory = new(directoryPath);
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                addDirectory(new LocalDirectorySnapshot
                {
                    RelativePath = relativePath,
                    FullPath = directory.FullName,
                });
                directoriesScanned++;
                ReportDirectoryScanProgress(progress, filesScanned, directoriesScanned, relativePath);
                pendingDirectories.Push(directory.FullName);
            }

            foreach (string filePath in Directory.EnumerateFiles(currentDirectory, "*", ChildEnumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = ToRelativePath(fullRoot, filePath);
                if (LocalFileIgnoreRules.ShouldIgnore(relativePath))
                {
                    continue;
                }

                FileInfo file = new(filePath);
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                LocalFileSnapshot fileSnapshot = await CreateSnapshotAsync(file, relativePath, computeHashes, cancellationToken)
                    .ConfigureAwait(false);
                addFile(fileSnapshot);
                filesScanned++;
                ReportScanProgress(progress, filesScanned, directoriesScanned, relativePath);
            }
        }

        progress?.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
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
            ? await ComputeHashAsync(file.FullName, relativePath, cancellationToken).ConfigureAwait(false)
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
        CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
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

    private readonly record struct LocalFileMetadata(long Length, DateTime LastWriteUtc);
}
