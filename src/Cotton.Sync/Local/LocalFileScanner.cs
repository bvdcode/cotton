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
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
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
            if (ShouldIgnore(relativePath))
            {
                continue;
            }

            FileInfo file = new(filePath);
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string contentHash = await ComputeHashAsync(file.FullName, cancellationToken).ConfigureAwait(false);
            snapshots.Add(new LocalFileSnapshot
            {
                RelativePath = relativePath,
                FullPath = file.FullName,
                ContentHash = contentHash,
                SizeBytes = file.Length,
                LastWriteUtc = file.LastWriteTimeUtc,
            });
        }

        snapshots.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        return snapshots;
    }

    private static bool ShouldIgnore(string relativePath)
    {
        string[] segments = relativePath.Split('/');
        if (segments.Any(x => string.Equals(x, ".cotton-sync", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string fileName = segments[^1];
        return fileName.StartsWith("~$", StringComparison.Ordinal)
            || fileName.EndsWith("~", StringComparison.Ordinal)
            || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativePath(string rootPath, string filePath)
    {
        string relative = Path.GetRelativePath(rootPath, filePath).Replace('\\', '/');
        return SyncPath.Normalize(relative);
    }

    private static async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
