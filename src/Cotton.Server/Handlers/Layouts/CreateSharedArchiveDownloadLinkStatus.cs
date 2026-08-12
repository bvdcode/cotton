// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Layouts
{
    /// <summary>
    /// Describes the outcome of resolving a shared archive request.
    /// </summary>
    public enum CreateSharedArchiveDownloadLinkStatus
    {
        /// <summary>
        /// The archive service produced a result.
        /// </summary>
        ArchiveResult,

        /// <summary>
        /// The public share token was not found or is no longer active.
        /// </summary>
        SharedFolderNotFound,

        /// <summary>
        /// The requested folder was not found inside the shared subtree.
        /// </summary>
        FolderNotFound,
    }
}
