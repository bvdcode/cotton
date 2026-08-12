// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Sdk.Notifications
{
    /// <summary>
    /// Represents a notification stored for the current user.
    /// </summary>
    public class CottonNotificationDto : BaseDto<Guid>
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = null!;

        /// <summary>
        /// Gets or sets the optional body.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the notification was read.
        /// </summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// Gets or sets structured notification metadata.
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the owning user identifier.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the notification priority.
        /// </summary>
        public CottonNotificationPriority Priority { get; set; }
    }
}
