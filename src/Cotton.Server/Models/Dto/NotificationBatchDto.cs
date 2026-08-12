// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class NotificationBatchDto
    {
        public List<NotificationDto> UnreadNotifications { get; set; } = [];

        public int UnreadCount { get; set; }

        public NotificationCursorDto? NextCursor { get; set; }
    }
}
