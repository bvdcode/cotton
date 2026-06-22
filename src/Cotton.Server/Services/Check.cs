// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models;
using Cotton.Models.Enums;
using Cotton.Server.Providers;

namespace Cotton.Server.Services
{
    /// <summary>
    /// Represents check.
    /// </summary>
    public class Check
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = null!;
        /// <summary>
        /// Gets or sets the operation status.
        /// </summary>
        public string Status { get; set; } = null!;
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = null!;
    }
}
