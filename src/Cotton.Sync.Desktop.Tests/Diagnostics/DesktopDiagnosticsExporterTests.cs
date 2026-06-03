// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.IO.Compression;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Diagnostics;

public sealed class DesktopDiagnosticsExporterTests
{
    private string _tempDirectory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cotton-diagnostics-" + Guid.NewGuid().ToString("N"));
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
    public async Task ExportAsync_CreatesArchiveWithDiagnosticsJsonAndLogs()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        File.WriteAllText(paths.LogFilePath, "sync log");
        var exporter = new DesktopDiagnosticsExporter();

        string archivePath = await exporter.ExportAsync(paths, CreateBundle());

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.Multiple(() =>
        {
            Assert.That(archive.GetEntry("diagnostics.json"), Is.Not.Null);
            Assert.That(archive.GetEntry("logs/cotton-sync.log"), Is.Not.Null);
        });
    }

    [Test]
    public async Task ExportAsync_DoesNotIncludeTokenStoreOrDatabases()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
        File.WriteAllText(paths.TokenStorePath, "secret-token");
        File.WriteAllText(paths.AppDatabasePath, "app-db");
        File.WriteAllText(paths.SyncStateDatabasePath, "sync-db");
        var exporter = new DesktopDiagnosticsExporter();

        string archivePath = await exporter.ExportAsync(paths, CreateBundle());

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        string[] entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(entryNames, Does.Not.Contain("tokens.json"));
            Assert.That(entryNames, Does.Not.Contain("sync-app.db"));
            Assert.That(entryNames, Does.Not.Contain("sync-state.db"));
        });
    }

    private static DesktopDiagnosticsBundle CreateBundle()
    {
        return new DesktopDiagnosticsBundle(
            DateTimeOffset.Parse("2026-06-03T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "1.0.0",
            "https://app.cottoncloud.dev/",
            "user@example.test",
            [
                new DesktopSyncPairSnapshot(
                    Guid.NewGuid(),
                    "Documents",
                    "/home/user/Documents",
                    "/Documents",
                    "Idle"),
            ],
            [
                new DesktopSelfTestItemSnapshot("Server identity", true, "Cotton Cloud"),
            ]);
    }
}
