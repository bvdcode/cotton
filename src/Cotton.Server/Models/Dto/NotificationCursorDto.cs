// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Dto
{
    public class NotificationCursorDto
    {
        public DateTime CreatedAt { get; set; }

        public Guid NotificationId { get; set; }
    }
}
