// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.Tests.Startup;

public sealed class DesktopStartupOptionsTests
{
    [Test]
    public void Parse_LoadsServerUrlAndUsername()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--server-url",
                "https://app.cottoncloud.dev/",
                "--username=desktop@example.test",
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(options.ServerUrl, Is.EqualTo(new Uri("https://app.cottoncloud.dev/")));
            Assert.That(options.Username, Is.EqualTo("desktop@example.test"));
            Assert.That(options.DataDirectory, Is.Null);
            Assert.That(options.StartMinimizedToTray, Is.False);
            Assert.That(options.RunSelfTest, Is.False);
        });
    }

    [Test]
    public void Parse_IgnoresUnsupportedServerUrl()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--server-url",
                "file:///tmp/cotton",
                "--username",
                " desktop@example.test ",
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(options.ServerUrl, Is.Null);
            Assert.That(options.Username, Is.EqualTo("desktop@example.test"));
        });
    }

    [Test]
    public void Parse_NormalizesBareServerHostToHttps()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--server",
                "app.cottoncloud.dev",
            ]);

        Assert.That(options.ServerUrl, Is.EqualTo(new Uri("https://app.cottoncloud.dev/")));
    }

    [Test]
    public void Parse_LoadsStartMinimizedFlag()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--start-minimized",
            ]);

        Assert.That(options.StartMinimizedToTray, Is.True);
    }

    [Test]
    public void Parse_LoadsSelfTestFlag()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--self-test",
            ]);

        Assert.That(options.RunSelfTest, Is.True);
    }

    [Test]
    public void Parse_LoadsExportDiagnosticsFlag()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--export-diagnostics",
            ]);

        Assert.That(options.ExportDiagnostics, Is.True);
    }

    [Test]
    public void Parse_LoadsVisualSmokeScenario()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--visual-smoke",
                "settings",
            ]);

        Assert.That(options.VisualSmokeScenario, Is.EqualTo(DesktopVisualSmokeScenario.Settings));
    }

    [Test]
    public void Parse_LoadsHyphenatedVisualSmokeScenario()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--visual-smoke",
                "add-folder",
            ]);

        Assert.That(options.VisualSmokeScenario, Is.EqualTo(DesktopVisualSmokeScenario.AddFolder));
    }

    [Test]
    public void Parse_LoadsMultiWordVisualSmokeScenario()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--visual-smoke",
                "settings-diagnostics",
            ]);

        Assert.That(options.VisualSmokeScenario, Is.EqualTo(DesktopVisualSmokeScenario.SettingsDiagnostics));
    }

    [Test]
    public void Parse_LoadsScreenshotStateAlias()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--screenshot-state=conflict",
            ]);

        Assert.That(options.VisualSmokeScenario, Is.EqualTo(DesktopVisualSmokeScenario.Conflict));
    }

    [Test]
    public void Parse_IgnoresUnsupportedVisualSmokeScenario()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--visual-smoke",
                "production",
            ]);

        Assert.That(options.VisualSmokeScenario, Is.Null);
    }

    [Test]
    public void Parse_LoadsDataDirectory()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--data-dir",
                " /tmp/cotton-sync-smoke ",
            ]);

        Assert.That(options.DataDirectory, Is.EqualTo("/tmp/cotton-sync-smoke"));
    }

    [Test]
    public void Parse_DoesNotTreatNextFlagAsOptionValue()
    {
        DesktopStartupOptions options = DesktopStartupOptions.Parse(
            [
                "--data-dir",
                "--self-test",
                "--server-url",
                "--username",
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(options.DataDirectory, Is.Null);
            Assert.That(options.ServerUrl, Is.Null);
            Assert.That(options.Username, Is.Null);
            Assert.That(options.RunSelfTest, Is.True);
        });
    }
}
