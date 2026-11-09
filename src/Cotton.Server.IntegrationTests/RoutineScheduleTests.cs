// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Handlers.Routines;

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
    }
}
