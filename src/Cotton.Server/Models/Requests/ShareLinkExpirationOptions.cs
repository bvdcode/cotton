// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models.Requests
{
    public class ShareLinkExpirationOptions
    {
        public int? ExpireAfterMinutes { get; set; } = ShareLinkExpiration.DefaultMinutes;
    }
}
