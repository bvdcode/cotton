// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Security.Cryptography;
using Cotton.Sync.State;

namespace Cotton.Sync.Local;

/// <summary>
/// Scans a local folder and hashes files for synchronization.
/// </summary>
public sealed class LocalFileScanner : ILocalFileScanner
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string fullRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Local sync root was not found: {fullRoot}");
        }

        var snapshots = new List<LocalFileSnapshot>();
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

            LocalFileSnapshot snapshot = await CreateSnapshotAsync(file, relativePath, cancellationToken).ConfigureAwait(false);
            snapshots.Add(snapshot);
        }

        snapshots.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        return snapshots;
    }

    private static string ToRelativePath(string rootPath, string filePath)
    {
        string relative = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        return SyncPath.Normalize(relative);
    }

    private static async Task<LocalFileSnapshot> CreateSnapshotAsync(
        FileInfo file,
        string relativePath,
        CancellationToken cancellationToken)
    {
        ValidatePlatformPermissions(file, relativePath);
        LocalFileMetadata before = ReadMetadata(file, relativePath);
        string contentHash = await ComputeHashAsync(file.FullName, relativePath, cancellationToken).ConfigureAwait(false);
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
                throw new LocalFileUnavailableException(
                    relativePath,
                    file.FullName,
                    "the file has no Unix read permission bits.");
            }
        }
        catch (LocalFileUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new LocalFileUnavailableException(relativePath, filePath, exception);
        }
    }

    private readonly record struct LocalFileMetadata(long Length, DateTime LastWriteUtc);
}
