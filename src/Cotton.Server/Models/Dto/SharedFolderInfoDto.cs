// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Public information about a shared folder, returned to unauthenticated users.
    /// </summary>
    public class SharedFolderInfoDto
    {
        /// <summary>
        /// Display name of the shared folder.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// The root node ID of the shared folder.
        /// </summary>
        public Guid NodeId { get; set; }

        /// <summary>
        /// When the share was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the share link expires (null = never).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }
}
