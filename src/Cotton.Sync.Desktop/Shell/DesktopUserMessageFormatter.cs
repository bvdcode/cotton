// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal static class DesktopUserMessageFormatter
    {
        public const int DefaultMaxLength = 180;

        public const int TitleMaxLength = 80;

        public static string Compact(string message, int maxLength = DefaultMaxLength)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 8);

            string trimmed = message.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed[..(maxLength - 3)].TrimEnd() + "...";
        }
    }
}
