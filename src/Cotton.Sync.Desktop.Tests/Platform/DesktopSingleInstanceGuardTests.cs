// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform
{
    public sealed class DesktopSingleInstanceGuardTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-single-instance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public void TryAcquire_ReturnsGuardForFreeLock()
        {
            using DesktopSingleInstanceGuard? guard = DesktopSingleInstanceGuard.TryAcquire(LockFilePath());

            Assert.That(guard, Is.Not.Null);
        }

        [Test]
        public void TryAcquire_ReturnsNullWhenLockAlreadyHeld()
        {
            using DesktopSingleInstanceGuard? first = DesktopSingleInstanceGuard.TryAcquire(LockFilePath());

            using DesktopSingleInstanceGuard? second = DesktopSingleInstanceGuard.TryAcquire(LockFilePath());

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Null);
            });
        }

        [Test]
        public void TryAcquire_AllowsAcquireAfterGuardDisposed()
        {
            string lockFilePath = LockFilePath();
            using (DesktopSingleInstanceGuard? first = DesktopSingleInstanceGuard.TryAcquire(lockFilePath))
            {
                Assert.That(first, Is.Not.Null);
            }

            using DesktopSingleInstanceGuard? second = DesktopSingleInstanceGuard.TryAcquire(lockFilePath);

            Assert.That(second, Is.Not.Null);
        }

        private string LockFilePath()
        {
            return Path.Combine(_tempDirectory, "cotton-sync.lock");
        }
    }
}
