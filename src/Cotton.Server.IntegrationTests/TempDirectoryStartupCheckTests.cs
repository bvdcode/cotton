// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Server.Services;
using Cotton.Server.Services.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Cotton.Server.IntegrationTests
{
    public class TempDirectoryStartupCheckTests
    {
        [Test]
        public async Task ValidateAsync_AllowsWritableTempDirectory()
        {
            string tempDirectory = CreateTestDirectory();

            StartupBlocker? blocker = await CreateCheck(tempDirectory)
                .ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Null);
        }

        [Test]
        public async Task ValidateAsync_BlocksWhenTempPathIsNotWritableDirectory()
        {
            string tempDirectory = CreateTestDirectory();
            string filePath = Path.Combine(tempDirectory, "not-a-directory");
            File.WriteAllText(filePath, string.Empty);

            StartupBlocker? blocker = await CreateCheck(filePath)
                .ValidateAsync(CancellationToken.None);

            Assert.That(blocker, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(blocker!.Kind, Is.EqualTo("temp-directory-not-writable"));
                Assert.That(blocker.Message, Does.Contain("read_only: true"));
                Assert.That(blocker.Message, Does.Contain("/tmp"));
            }
        }

        private static TempDirectoryStartupCheck CreateCheck(string tempPath)
        {
            return new TempDirectoryStartupCheck(
                new TempDirectoryProbe(() => tempPath),
                NullLogger<TempDirectoryStartupCheck>.Instance);
        }

        private static string CreateTestDirectory()
        {
            string tempDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "temp-directory-startup-checks",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}
