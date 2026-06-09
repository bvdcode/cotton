// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Sync.Desktop.Platform;

namespace Cotton.Sync.Desktop.Tests.Platform
{
    public class WindowsToastNotificationServiceTests
    {
        [Test]
        public void CreateStartInfo_UsesEncodedPowerShellCommandWithoutShell()
        {
            ProcessStartInfo startInfo = WindowsToastNotificationService.CreateStartInfo(
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                "Action required",
                "Bob's folder needs attention");

            string encodedCommand = startInfo.ArgumentList.Last();
            string command = WindowsToastNotificationService.DecodePowerShellCommand(encodedCommand);

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"));
                Assert.That(startInfo.UseShellExecute, Is.False);
                Assert.That(startInfo.CreateNoWindow, Is.True);
                Assert.That(startInfo.ArgumentList, Is.EqualTo(new[]
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-EncodedCommand",
                    encodedCommand,
                }));
                Assert.That(command, Does.Contain("ToastNotificationManager"));
                Assert.That(command, Does.Contain("CreateToastNotifier('Cotton.Sync.Desktop')"));
                Assert.That(command, Does.Contain("$bindingNode.SetAttribute('template', 'ToastGeneric')"));
                Assert.That(command, Does.Not.Contain("ToastText02"));
                Assert.That(command, Does.Contain("$null = $titleNode.AppendChild($xml.CreateTextNode('Action required'))"));
                Assert.That(command, Does.Contain("$null = $messageNode.AppendChild($xml.CreateTextNode('Bob''s folder needs attention'))"));
                Assert.That(command, Does.Not.Contain("appLogoOverride"));
            });
        }

        [Test]
        public void CreateStartInfo_WithIconAddsToastAppLogoOverride()
        {
            string iconPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Assets", "icon-192.png");
            ProcessStartInfo startInfo = WindowsToastNotificationService.CreateStartInfo(
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                "Signed in",
                "desktop@example.test",
                iconPath);

            string command = WindowsToastNotificationService.DecodePowerShellCommand(startInfo.ArgumentList.Last());
            string expectedIconUri = new Uri(Path.GetFullPath(iconPath)).AbsoluteUri;

            Assert.Multiple(() =>
            {
                Assert.That(command, Does.Contain("$imageNode.SetAttribute('placement', 'appLogoOverride')"));
                Assert.That(command, Does.Contain("$imageNode.SetAttribute('src', '" + expectedIconUri + "')"));
                Assert.That(command, Does.Not.Contain("$bindingNode = $xml.GetElementsByTagName('binding').Item(0)"));
                Assert.That(command, Does.Contain("$null = $bindingNode.AppendChild($imageNode)"));
                Assert.That(command, Does.Contain("$null = $bindingNode.AppendChild($titleNode)"));
                Assert.That(command, Does.Contain("$null = $bindingNode.AppendChild($messageNode)"));
            });
        }
    }
}
