// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Nodes;
using System.Text.Json.Serialization;

namespace Cotton.Files
{
    /// <summary>
    /// Lists the supported restore conflict kind values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RestoreConflictKind
    {
        /// <summary>
        /// Represents the folder option.
        /// </summary>
        Folder = 0,

        /// <summary>
        /// Represents the file option.
        /// </summary>
        File = 1,
    }
}
