// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
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
    ILocalFileContentHasher
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private const int ProgressReportFileInterval = 100;
    private static readonly EnumerationOptions FileEnumerationOptions = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false,
        RecurseSubdirectories = true,
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
        return await ScanTreeCoreAsync(rootPath, computeHashes: true, progress: null, cancellationToken).ConfigureAwait(false);
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
        return await ScanTreeCoreAsync(rootPath, computeHashes: false, progress, cancellationToken).ConfigureAwait(false);
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

    private static async Task<LocalTreeSnapshot> ScanTreeCoreAsync(
        string rootPath,
        bool computeHashes,
        IProgress<LocalTreeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Local sync root was not found: {fullRoot}");
        }

        var tree = new LocalTreeSnapshot();
        int directoriesScanned = 0;
        int filesScanned = 0;
        progress?.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
        foreach (string directoryPath in Directory.EnumerateDirectories(fullRoot, "*", FileEnumerationOptions))
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

            tree.Directories.Add(new LocalDirectorySnapshot
            {
                RelativePath = relativePath,
                FullPath = directory.FullName,
            });
            directoriesScanned++;
        }

        foreach (string filePath in Directory.EnumerateFiles(fullRoot, "*", FileEnumerationOptions))
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
            tree.Files.Add(fileSnapshot);
            filesScanned++;
            ReportScanProgress(progress, filesScanned, directoriesScanned, relativePath);
        }

        tree.Directories.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        tree.Files.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        progress?.Report(new LocalTreeScanProgress(filesScanned, directoriesScanned, currentPath: null));
        return tree;
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

        if (filesScanned == 1 || filesScanned % ProgressReportFileInterval == 0)
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
            await using FileStream stream = File.OpenRead(filePath);
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
