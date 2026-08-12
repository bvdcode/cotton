// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class UserExternalIdentityDto : BaseDto<Guid>
    {
        public Guid ProviderId { get; set; }

        public string ProviderName { get; set; } = null!;

        public string ProviderSlug { get; set; } = null!;

        public string? Email { get; set; }

        public bool EmailVerified { get; set; }

        public string? DisplayName { get; set; }

        public string? PictureUrl { get; set; }

        public DateTime? LastUsedAt { get; set; }
    }
}
