// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using System.Globalization;

namespace Cotton.Server.Services
{
    internal static class SecurityEmailTimestampFormatter
    {
        public static string Format(DateTime occurredAt, TimeZoneInfo timeZone)
        {
            DateTime occurredAtUtc = occurredAt.Kind switch
            {
                DateTimeKind.Utc => occurredAt,
                DateTimeKind.Local => occurredAt.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
                _ => throw new ArgumentOutOfRangeException(nameof(occurredAt)),
            };
            DateTimeOffset localTimestamp = TimeZoneInfo.ConvertTime(
                new DateTimeOffset(occurredAtUtc),
                timeZone);

            return localTimestamp.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)
                + " ("
                + timeZone.Id
                + ")";
        }
    }
}
