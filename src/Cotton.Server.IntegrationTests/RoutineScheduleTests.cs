// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Routines;
using Cotton.Routines;

namespace Cotton.Server.IntegrationTests
{
    public class RoutineScheduleTests
    {
        [Test]
        public void NewRoutineIsDueImmediately()
        {
            Assert.That(RoutineSchedule.IsDue(null, TimeSpan.FromDays(365), DateTime.UtcNow), Is.True);
        }

        [TestCase(1)]
        [TestCase(24)]
        [TestCase(168)]
        [TestCase(720)]
        [TestCase(8760)]
        public void IntervalHasAnInclusiveBoundary(int hours)
        {
            DateTime last = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            TimeSpan interval = TimeSpan.FromHours(hours);
            Assert.That(RoutineSchedule.IsDue(last, interval, last + interval - TimeSpan.FromTicks(1)), Is.False);
            Assert.That(RoutineSchedule.IsDue(last, interval, last + interval), Is.True);
            Assert.That(RoutineSchedule.IsDue(last, interval, last + interval + interval), Is.True);
        }

        [Test]
        public void SettingsSignatureIgnoresJsonPropertyOrderButProtectsDeletionPolicy()
        {
            Routine routine = new()
            {
                Name = "Cleanup", Interval = TimeSpan.FromDays(1),
                Settings = "{\"OlderThanDays\":30,\"DateField\":0,\"IncludeSubfolders\":true,\"PermanentlyDelete\":false}"
            };
            RoutineIntegrityDescriptor descriptor = new();
            byte[] original = descriptor.BuildCanonicalPayload(routine);
            routine.Settings = "{\"PermanentlyDelete\":false,\"IncludeSubfolders\":true,\"DateField\":0,\"OlderThanDays\":30}";
            Assert.That(descriptor.BuildCanonicalPayload(routine), Is.EqualTo(original));
            routine.Settings = routine.Settings.Replace("false", "true");
            Assert.That(descriptor.BuildCanonicalPayload(routine), Is.Not.EqualTo(original));
        }

        [Test]
        public void CalendarDoesNotRepeatTheFallBackHour()
        {
            TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            RoutineCalendarSettings calendar = new() { Frequency = RoutineCalendarFrequency.Daily, Hour = 1, DayOfMonth = 1, Month = 1 };
            DateTime firstOne = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
            DateTime? next = RoutineSchedule.NextCalendar(firstOne, calendar, timezone);
            Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc)));
            Assert.That(RoutineSchedule.IsDue(firstOne, TimeSpan.FromHours(1), firstOne.AddHours(1)), Is.True);
        }

        [Test]
        public void CalendarUsesServerTimezoneAndSkipsMissingDates()
        {
            RoutineCalendarSettings calendar = new() { Frequency = RoutineCalendarFrequency.Monthly, Hour = 9, DayOfMonth = 31, Month = 1 };
            DateTime? next = RoutineSchedule.NextCalendar(new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc), calendar, TimeZoneInfo.Utc);
            Assert.That(next, Is.EqualTo(new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void SpringForwardDoesNotCreateANonexistentCalendarTime()
        {
            TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
            RoutineCalendarSettings calendar = new() { Frequency = RoutineCalendarFrequency.Daily, Hour = 2, Minute = 30 };
            DateTime? next = RoutineSchedule.NextCalendar(new DateTime(2026, 3, 7, 10, 30, 0, DateTimeKind.Utc), calendar, timezone);
            Assert.That(next, Is.EqualTo(new DateTime(2026, 3, 9, 9, 30, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void DelayedStartAndDisabledRoutineAreNotDue()
        {
            DateTime now = DateTime.UtcNow;
            Routine routine = new() { Enabled = true, StartAt = now.AddDays(1) };
            Assert.That(RoutineSchedule.NextExecution(routine, TimeZoneInfo.Utc), Is.EqualTo(routine.StartAt));
            routine.Enabled = false;
            Assert.That(RoutineSchedule.NextExecution(routine, TimeZoneInfo.Utc), Is.Null);
        }
    }
}
