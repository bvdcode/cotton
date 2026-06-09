// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Platform
{

    /// <summary>
    /// Runs host platform commands requested by the application layer.
    /// </summary>
    public interface IPlatformCommandService
    {
        /// <summary>
        /// Opens a local folder in the host file manager.
        /// </summary>
        Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a URL in the default browser.
        /// </summary>
        Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default);
    }
}
