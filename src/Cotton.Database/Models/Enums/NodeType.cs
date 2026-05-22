// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>Separates independent node trees that must not be mixed as parent and child.</summary>
    public enum NodeType
    {
        /// <summary>Normal user-visible file tree.</summary>
        Default = 0,
        /// <summary>Trash file tree.</summary>
        Trash = 1,
    }
}
