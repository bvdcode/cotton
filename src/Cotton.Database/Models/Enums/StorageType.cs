// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>
    /// Selects the physical storage backend.
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// Use local server resources.
        /// </summary>
        Local = 0,
        /// <summary>
        /// Use S3-compatible object storage.
        /// </summary>
        S3 = 1,
    }
}
