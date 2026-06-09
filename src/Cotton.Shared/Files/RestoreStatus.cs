// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Text.Json.Serialization;

namespace Cotton.Files
{
    /// <summary>
    /// Lists the supported restore status values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreStatus
    {
        /// <summary>
        /// The item was restored successfully.
        /// </summary>
        Restored = 0,

        /// <summary>
        /// The original parent path is missing and was not recreated.
        /// </summary>
        ParentMissing = 1,

        /// <summary>
        /// Another item already occupies the restore destination.
        /// </summary>
        Conflict = 2,

        /// <summary>
        /// The item cannot be restored from its current state.
        /// </summary>
        NotRestorable = 3,
    }
}
