// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Diagnostics;

namespace Cotton.Sync.Desktop.Tests.Diagnostics
{

    public sealed class RotatingFileTraceListenerTests
    {
        private string _tempDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-trace-log-" + Guid.NewGuid().ToString("N"));
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
        public void WriteLine_CreatesLogFile()
        {
            string path = LogPath();
            using var listener = new RotatingFileTraceListener(path, maxFileSizeBytes: 1024);

            listener.WriteLine("sync started");

            Assert.That(File.ReadAllText(path), Does.Contain("sync started"));
        }

        [Test]
        public void WriteLine_RotatesExistingLogWhenSizeLimitIsExceeded()
        {
            string path = LogPath();
            File.WriteAllText(path, new string('a', 80));
            using var listener = new RotatingFileTraceListener(path, maxFileSizeBytes: 96, retainedFileCount: 2);

            listener.WriteLine("sync started");

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(path), Does.Contain("sync started"));
                Assert.That(File.ReadAllText(path + ".1"), Does.Contain(new string('a', 80)));
            });
        }

        [Test]
        public void WriteLine_RetainsConfiguredNumberOfRotatedFiles()
        {
            string path = LogPath();
            File.WriteAllText(path, "current");
            File.WriteAllText(path + ".1", "previous");
            File.WriteAllText(path + ".2", "oldest");
            using var listener = new RotatingFileTraceListener(path, maxFileSizeBytes: 1, retainedFileCount: 2);

            listener.WriteLine("next");

            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(path), Does.Contain("next"));
                Assert.That(File.ReadAllText(path + ".1"), Does.Contain("current"));
                Assert.That(File.ReadAllText(path + ".2"), Does.Contain("previous"));
            });
        }

        private string LogPath()
        {
            return Path.Combine(_tempDirectory, "cotton-sync.log");
        }
    }
}
