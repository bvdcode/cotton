// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Services.Search
{
    /// <summary>
    /// Defines the kind of entity returned by a layout search provider.
    /// </summary>
    public enum LayoutSearchHitKind
    {
        /// <summary>
        /// A folder-like node hit.
        /// </summary>
        Node = 0,

        /// <summary>
        /// A visible file entry hit.
        /// </summary>
        File = 1,
    }
}
