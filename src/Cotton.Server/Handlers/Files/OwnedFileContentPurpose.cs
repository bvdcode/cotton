// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Handlers.Files
{
    /// <summary>
    /// Identifies how owned file content will be consumed.
    /// </summary>
    public enum OwnedFileContentPurpose
    {
        /// <summary>
        /// The file bytes will be downloaded.
        /// </summary>
        Download,

        /// <summary>
        /// The ordered content manifest will be returned.
        /// </summary>
        Manifest,
    }
}
