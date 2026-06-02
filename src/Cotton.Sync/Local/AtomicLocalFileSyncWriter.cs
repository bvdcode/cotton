// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;

namespace Cotton.Sync.Local;

/// <summary>
/// Writes synchronized local files through temporary files under the sync metadata folder.
/// </summary>
public sealed class AtomicLocalFileSyncWriter : ILocalFileSyncWriter
{
    private const string MetadataDirectoryName = ".cotton-sync";
    private const string TemporaryDirectoryName = "tmp";

    /// <inheritdoc />
    public async Task WriteFileAsync(
        string rootPath,
        string relativePath,
        Func<Stream, CancellationToken, Task> writeContentAsync,
        DateTime? lastWriteUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(writeContentAsync);
        string normalizedPath = SyncPath.Normalize(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(fullRoot);

        string targetPath = BuildSafeTargetPath(fullRoot, normalizedPath);
        string? targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string temporaryDirectory = Path.Combine(fullRoot, MetadataDirectoryName, TemporaryDirectoryName);
        Directory.CreateDirectory(temporaryDirectory);
        string temporaryPath = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N") + ".download");
        bool moved = false;
        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await writeContentAsync(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, targetPath, overwrite: true);
            moved = true;
            if (lastWriteUtc.HasValue)
            {
                File.SetLastWriteTimeUtc(targetPath, lastWriteUtc.Value.ToUniversalTime());
            }
        }
        finally
        {
            if (!moved && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = SyncPath.Normalize(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        string targetPath = BuildSafeTargetPath(fullRoot, normalizedPath);
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            DeleteEmptyParents(fullRoot, Path.GetDirectoryName(targetPath));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string CreateConflictRelativePath(string rootPath, string relativePath, DateTime timestampUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string normalizedPath = SyncPath.Normalize(relativePath);
        string directory = Path.GetDirectoryName(normalizedPath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        string extension = Path.GetExtension(normalizedPath);
        string suffix = timestampUtc.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        for (int index = 1; index < int.MaxValue; index++)
        {
            string indexedSuffix = index == 1 ? suffix : suffix + "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string candidateName = fileName + " (Cotton conflict " + indexedSuffix + ")" + extension;
            string candidateRelativePath = string.IsNullOrEmpty(directory)
                ? candidateName
                : directory.Replace(Path.DirectorySeparatorChar, '/') + "/" + candidateName;
            string candidateFullPath = BuildSafeTargetPath(Path.GetFullPath(rootPath), candidateRelativePath);
            if (!File.Exists(candidateFullPath))
            {
                return SyncPath.Normalize(candidateRelativePath);
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique conflict file path.");
    }

    private static string BuildSafeTargetPath(string fullRoot, string normalizedRelativePath)
    {
        string root = Path.GetFullPath(fullRoot);
        string rootPrefix = EndsWithDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        string targetPath = Path.GetFullPath(Path.Combine(
            root,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!targetPath.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new ArgumentException("Relative path must stay inside the sync root.", nameof(normalizedRelativePath));
        }

        return targetPath;
    }

    private static bool EndsWithDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar);
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static void DeleteEmptyParents(string fullRoot, string? currentDirectory)
    {
        string root = Path.GetFullPath(fullRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrEmpty(currentDirectory))
        {
            string current = Path.GetFullPath(currentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)
                || current.EndsWith(Path.DirectorySeparatorChar + MetadataDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (Directory.EnumerateFileSystemEntries(current).Any())
            {
                break;
            }

            Directory.Delete(current);
            currentDirectory = Path.GetDirectoryName(current);
        }
    }
}
