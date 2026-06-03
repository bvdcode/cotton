// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cotton.Sync.App.Platform;

/// <summary>
/// Runs platform open commands through the host shell.
/// </summary>
public sealed class ProcessPlatformCommandService : IPlatformCommandService
{
    private readonly ILogger<ProcessPlatformCommandService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessPlatformCommandService" /> class.
    /// </summary>
    public ProcessPlatformCommandService(ILogger<ProcessPlatformCommandService>? logger = null)
    {
        _logger = logger ?? NullLogger<ProcessPlatformCommandService>.Instance;
    }

    /// <inheritdoc />
    public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        StartShellCommand(localPath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("URL must be absolute.", nameof(url));
        }

        StartShellCommand(url.AbsoluteUri);
        return Task.CompletedTask;
    }

    private void StartShellCommand(string target)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (IsExpectedShellFailure(exception))
        {
            _logger.LogError(exception, "Failed to open shell target: {ShellTarget}", target);
            throw;
        }
    }

    private static bool IsExpectedShellFailure(Exception exception)
    {
        return exception is Win32Exception
            or FileNotFoundException
            or InvalidOperationException
            or ObjectDisposedException;
    }
}
