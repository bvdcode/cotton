// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using NUnit.Framework;
using System.Globalization;

namespace Cotton.Server.IntegrationTests
{
    public class SecurityEmailTimestampFormatterTests
    {
        [TestCase("2026-01-15T20:00:00Z", "2026-01-15 12:00:00 -08:00 (America/Los_Angeles)")]
        [TestCase("2026-07-11T09:00:00Z", "2026-07-11 02:00:00 -07:00 (America/Los_Angeles)")]
        public void Format_UsesConfiguredTimeZone(string occurredAtText, string expected)
        {
            DateTime occurredAt = DateTime.Parse(
                occurredAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

            string result = SecurityEmailTimestampFormatter.Format(occurredAt, timeZone);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Format_TreatsUnspecifiedEventTimeAsUtc()
        {
            DateTime occurredAt = new DateTime(2026, 7, 11, 9, 0, 0, DateTimeKind.Unspecified);
            TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

            string result = SecurityEmailTimestampFormatter.Format(occurredAt, timeZone);

            Assert.That(result, Is.EqualTo("2026-07-11 02:00:00 -07:00 (America/Los_Angeles)"));
        }
    }
}
