// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Processors
{
    /// <summary>
    /// Provides the Zstandard compression level used for newly written storage blobs.
    /// </summary>
    public interface ICompressionLevelProvider
    {
        /// <summary>
        /// Gets the current Zstandard compression level.
        /// </summary>
        int Level { get; }
    }
}
