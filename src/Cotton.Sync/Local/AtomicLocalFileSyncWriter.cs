// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Globalization;
using Cotton.Sync.State;

namespace Cotton.Sync.Local;

/// <summary>
/// Writes synchronized local files through temporary files under the sync metadata folder.
/// </summary>
public sealed class AtomicLocalFileSyncWriter : ILocalFileSyncWriter
{
    private const string MetadataDirectoryName = ".cotton-sync";
    private const string DeletedDirectoryName = "deleted";
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
        string normalizedPath = NormalizeWritablePath(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(fullRoot);

        string targetPath = Path.Combine(fullRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        string? targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        string temporaryDirectory = Path.Combine(fullRoot, MetadataDirectoryName, TemporaryDirectoryName);
        Directory.CreateDirectory(temporaryDirectory);
        CleanupTemporaryDownloads(temporaryDirectory);
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
        string normalizedPath = NormalizeWritablePath(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        string targetPath = Path.Combine(fullRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(targetPath))
        {
            string preservedPath = CreateDeletedPath(fullRoot, normalizedPath);
            string? preservedDirectory = Path.GetDirectoryName(preservedPath);
            if (!string.IsNullOrWhiteSpace(preservedDirectory))
            {
                Directory.CreateDirectory(preservedDirectory);
            }

            File.Move(targetPath, preservedPath, overwrite: false);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateDirectoryAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = NormalizeWritablePath(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(Path.Combine(fullRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteDirectoryAsync(string rootPath, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = NormalizeWritablePath(relativePath);
        string fullRoot = Path.GetFullPath(rootPath);
        string targetPath = Path.Combine(fullRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(targetPath))
        {
            string preservedPath = CreateDeletedPath(fullRoot, normalizedPath);
            string? preservedParentDirectory = Path.GetDirectoryName(preservedPath);
            if (!string.IsNullOrWhiteSpace(preservedParentDirectory))
            {
                Directory.CreateDirectory(preservedParentDirectory);
            }

            Directory.Move(targetPath, preservedPath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string CreateConflictRelativePath(string rootPath, string relativePath, DateTime timestampUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string normalizedPath = NormalizeWritablePath(relativePath);
        string directory = Path.GetDirectoryName(normalizedPath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        string extension = Path.GetExtension(normalizedPath);
        string suffix = timestampUtc.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        for (int index = 1; index < int.MaxValue; index++)
        {
            string indexedSuffix = index == 1 ? suffix : suffix + "-" + index.ToString(CultureInfo.InvariantCulture);
            string candidateName = fileName + " (Cotton conflict " + indexedSuffix + ")" + extension;
            string candidateRelativePath = string.IsNullOrEmpty(directory)
                ? candidateName
                : directory.Replace(Path.DirectorySeparatorChar, '/') + "/" + candidateName;
            string candidateFullPath = Path.Combine(
                Path.GetFullPath(rootPath),
                candidateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidateFullPath) && !Directory.Exists(candidateFullPath))
            {
                return SyncPath.Normalize(candidateRelativePath);
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique conflict file path.");
    }

    private static string NormalizeWritablePath(string relativePath)
    {
        string normalizedPath = SyncPath.Normalize(relativePath);
        if (SyncPathIgnoreRules.ShouldIgnore(normalizedPath))
        {
            throw new ArgumentException("Ignored sync paths cannot be written by the local sync writer.", nameof(relativePath));
        }

        return normalizedPath;
    }

    private static void CleanupTemporaryDownloads(string temporaryDirectory)
    {
        foreach (string temporaryFile in Directory.EnumerateFiles(temporaryDirectory, "*.download"))
        {
            try
            {
                File.Delete(temporaryFile);
            }
            catch (IOException exception)
            {
                Trace.TraceWarning("Failed to delete stale sync download temp file '{0}': {1}", temporaryFile, exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Trace.TraceWarning("Failed to delete stale sync download temp file '{0}': {1}", temporaryFile, exception.Message);
            }
        }
    }

    private static string CreateDeletedPath(string fullRoot, string normalizedPath)
    {
        string quarantineName = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture)
            + "-"
            + Guid.NewGuid().ToString("N");
        return Path.Combine(
            fullRoot,
            MetadataDirectoryName,
            DeletedDirectoryName,
            quarantineName,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
