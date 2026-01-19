// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Cotton.Database.Models.Enums;
using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserDto : BaseDto<Guid>
    {
        public string Username { get; set; } = null!;
        public UserRole Role { get; set; }
        public bool IsTotpEnabled { get; set; }
        public DateTime? TotpEnabledAt { get; set; }
        public int TotpFailedAttempts { get; set; }
    }
}
