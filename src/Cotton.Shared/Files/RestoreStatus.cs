// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
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
        /// The node was successfully restored to its original location.
        /// </summary>
        Restored = 0,

        /// <summary>
        /// The original parent folder no longer exists, so the node could not be restored in place.
        /// </summary>
        ParentMissing = 1,

        /// <summary>
        /// A node with the same name already exists at the target location.
        /// </summary>
        Conflict = 2,

        /// <summary>
        /// The node is not eligible for restore.
        /// </summary>
        NotRestorable = 3,
    }
}
