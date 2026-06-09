// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Files;

namespace Cotton.Sync.Remote
{

    /// <summary>
    /// Represents one remote file discovered during a remote tree crawl.
    /// </summary>
    public sealed class RemoteFileSnapshot
    {
        /// <summary>
        /// Gets or sets the normalized relative path.
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the remote file DTO.
        /// </summary>
        public NodeFileManifestDto File { get; set; } = new();
    }
}
