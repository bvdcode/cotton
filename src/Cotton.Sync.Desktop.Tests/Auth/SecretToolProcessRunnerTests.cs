// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Security.Cryptography;
using Cotton.Sync.Desktop.Auth;

namespace Cotton.Sync.Desktop.Tests.Auth
{
    public class SecretToolProcessRunnerTests
    {
        [Test]
        public async Task ReadAsync_TimesOutHangingHelper()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Pass("Hanging helper timeout coverage uses the Linux shell.");
                return;
            }

            var runner = new SecretToolProcessRunner(TimeSpan.FromMilliseconds(100));
            var startInfo = new ProcessStartInfo("/bin/sh");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("sleep 5");

            CryptographicException? exception = Assert.ThrowsAsync<CryptographicException>(
                async () => await runner.ReadAsync(startInfo, CancellationToken.None));

            Assert.That(exception?.Message, Does.Contain("timed out"));
        }
    }
}
