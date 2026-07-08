// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Storage.Abstractions
{
    /// <summary>
    /// Describes how storage writes treat an already existing object key.
    /// </summary>
    public enum StorageWriteMode
    {
        /// <summary>
        /// Writes only when the object does not already exist.
        /// </summary>
        CreateIfMissing = 0,

        /// <summary>
        /// Replaces an existing object.
        /// </summary>
        [Obsolete("OBSOLETE TRANSITION: force overwrite exists only for the CTN2 rewrite job. Remove it with that job.")]
        OverwriteExisting = 1,
    }
}
