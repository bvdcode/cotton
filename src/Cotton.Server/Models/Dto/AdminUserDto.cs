// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using EasyExtensions.Models.Enums;

namespace Cotton.Server.Models.Dto
{
    public class AdminUserDto : BaseDto<Guid>
    {
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateOnly? BirthDate { get; set; }
        public UserRole Role { get; set; }
        public bool IsTotpEnabled { get; set; }
        public DateTime? TotpEnabledAt { get; set; }
        public int TotpFailedAttempts { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int ActiveSessionCount { get; set; }
    }
}
