// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

namespace Cotton.Server.Models
{
    public static class ShareLinkExpiration
    {
        public const int DefaultMinutes = 60 * 24;
        public const int MaximumMinutes = 60 * 24 * 365 * 10;

        public static DateTime? FromMinutes(int? expireAfterMinutes)
        {
            if (expireAfterMinutes is not int minutes)
            {
                return null;
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan(minutes, MaximumMinutes, nameof(expireAfterMinutes));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minutes, nameof(expireAfterMinutes));
            return DateTime.UtcNow.AddMinutes(minutes);
        }
    }
}
