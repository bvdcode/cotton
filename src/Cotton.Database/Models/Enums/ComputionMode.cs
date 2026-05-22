// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Database.Models.Enums
{
    /// <summary>Selects where compute-heavy operations should run.</summary>
    public enum ComputionMode
    {
        /// <summary>Use local server resources.</summary>
        Local = 0,
        /// <summary>Use Cotton cloud services for the feature.</summary>
        Cloud = 1,
        /// <summary>Use a remote compute service.</summary>
        Remote = 2,
    }
}
