// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform
{
    public sealed class DesktopSingleInstanceActivationTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-single-instance-activation-" + Guid.NewGuid().ToString("N"));
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
        public void CreatePipeName_ReturnsStableSafeNameForLockPath()
        {
            string lockFilePath = LockFilePath();

            string first = DesktopSingleInstanceActivation.CreatePipeName(lockFilePath);
            string second = DesktopSingleInstanceActivation.CreatePipeName(lockFilePath);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(second));
                Assert.That(first, Does.StartWith("cotton-sync-"));
                Assert.That(first, Does.Not.Contain(Path.DirectorySeparatorChar.ToString()));
                Assert.That(first, Does.Not.Contain(Path.AltDirectorySeparatorChar.ToString()));
            });
        }

        [Test]
        public async Task TryRequestShowAsync_NotifiesRunningServer()
        {
            using var activated = new ManualResetEventSlim(initialState: false);
            using DesktopSingleInstanceActivationServer server = DesktopSingleInstanceActivation.StartServer(
                LockFilePath(),
                () => activated.Set());

            bool requested = await DesktopSingleInstanceActivation.TryRequestShowAsync(
                LockFilePath(),
                TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(requested, Is.True);
                Assert.That(activated.Wait(TimeSpan.FromSeconds(2)), Is.True);
            });
        }

        [Test]
        public async Task TryRequestShowAsync_ReturnsFalseWhenNoServerIsListening()
        {
            bool requested = await DesktopSingleInstanceActivation.TryRequestShowAsync(
                LockFilePath(),
                TimeSpan.FromMilliseconds(100));

            Assert.That(requested, Is.False);
        }

        private string LockFilePath()
        {
            return Path.Combine(_tempDirectory, "cotton-sync.lock");
        }
    }
}
