// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    /// <summary>
    /// Represents the notification API payload.
    /// </summary>
    public class NotificationDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets title.
        /// </summary>
        public string Title { get; set; } = null!;

        /// <summary>
        /// Gets or sets content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets read at.
        /// </summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// Gets or sets structured metadata attached to the resource.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets user id.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets priority.
        /// </summary>
        public NotificationPriority Priority { get; set; }
    }
}
