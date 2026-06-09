// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Files
{
    /// <summary>
    /// Lists the resource kinds that can block a restore destination.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreConflictKind
    {
        /// <summary>
        /// A folder blocks the restore destination.
        /// </summary>
        Folder = 0,

        /// <summary>
        /// A file blocks the restore destination.
        /// </summary>
        File = 1,
    }
}
