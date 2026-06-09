// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Cli
{

    internal static class SyncCliFormat
    {
        public static string FormatActivityDetails(string? details)
        {
            return string.IsNullOrWhiteSpace(details) ? string.Empty : " - " + details;
        }

        public static string FormatUtc(DateTime value)
        {
            return DateTime
                .SpecifyKind(value, DateTimeKind.Utc)
                .ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToStringInvariant(this int value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToStringInvariant(this long value)
        {
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToStringInvariant(this double value)
        {
            return value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
