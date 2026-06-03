// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.Tests.Startup;

public sealed class DesktopCommandLineRunnerTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-desktop-cli-" + Guid.NewGuid().ToString("N"));
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
    public async Task RunSelfTestAsync_PrintsReportAndReturnsSuccessWhenChecksPass()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(["--data-dir", _tempDirectory]);
        using var output = new StringWriter();

        int exitCode = await DesktopCommandLineRunner.RunSelfTestAsync(options, output);

        string report = output.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(report, Does.Contain("Cotton Sync Desktop self-test"));
            Assert.That(report, Does.Contain("[OK] Preferences database"));
            Assert.That(report, Does.Contain("Result: passed"));
        });
    }
}
