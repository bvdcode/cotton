// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    /// <summary>
    /// Represents the restore item request request payload accepted by the API.
    /// </summary>
    public class RestoreItemRequest
    {
        /// <summary>
        /// Creates missing parents.
        /// </summary>
        public bool CreateMissingParents { get; set; }
        /// <summary>
        /// Gets or sets whether restore should move an existing conflicting item to trash.
        /// </summary>
        public bool Overwrite { get; set; }
    }
}
