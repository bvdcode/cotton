// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.SyncPairs;

/// <summary>
/// Checks local sync roots using the host file system.
/// </summary>
public sealed class FileSystemLocalSyncRootProbe : ILocalSyncRootProbe
{
    private readonly ILogger<FileSystemLocalSyncRootProbe> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemLocalSyncRootProbe" /> class.
    /// </summary>
    public FileSystemLocalSyncRootProbe(ILogger<FileSystemLocalSyncRootProbe>? logger = null)
    {
        _logger = logger ?? NullLogger<FileSystemLocalSyncRootProbe>.Instance;
    }

    /// <inheritdoc />
    public Task<bool> CanUseAsync(string localRootPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(localRootPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            DirectoryInfo directory = Directory.CreateDirectory(localRootPath);
            SyncMetadataDirectory.HideIfExists(directory.FullName);
            bool canUse = directory.Exists;
            return Task.FromResult(canUse);
        }
        catch (Exception exception) when (IsExpectedFileSystemFailure(exception))
        {
            _logger.LogWarning(
                exception,
                "Local sync root is unavailable: {LocalRootPath}",
                localRootPath);
            return Task.FromResult(false);
        }
    }

    private static bool IsExpectedFileSystemFailure(Exception exception)
    {
        return exception is ArgumentException
            or IOException
            or NotSupportedException
            or UnauthorizedAccessException;
    }
}
