// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>Selects how Cotton sends email.</summary>
    public enum EmailMode
    {
        /// <summary>Disable the feature.</summary>
        None = 0,
        /// <summary>Use Cotton Bridge services for the feature.</summary>
        Cloud = 1,
        /// <summary>Use administrator-provided custom settings.</summary>
        Custom = 2,
    }
}
