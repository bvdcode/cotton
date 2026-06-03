// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform;

public sealed class NotifySendNotificationServiceTests
{
    [Test]
    public void CreateStartInfo_UsesNotifySendArgumentsWithoutShell()
    {
        ProcessStartInfo startInfo = NotifySendNotificationService.CreateStartInfo(
            "/usr/bin/notify-send",
            "Action required",
            "Documents: upload failed");

        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("/usr/bin/notify-send"));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.CreateNoWindow, Is.True);
            Assert.That(startInfo.ArgumentList, Is.EqualTo(new[]
            {
                "--app-name",
                "Cotton Sync",
                "Action required",
                "Documents: upload failed",
            }));
        });
    }
}
