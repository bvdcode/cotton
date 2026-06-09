// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.IO.Compression;
using System.Text.Json;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.Tests.Diagnostics
{
    public class DesktopDiagnosticsExporterTests
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

            string archivePath = await exporter.ExportAsync(paths, CreateBundle(paths));

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

            string archivePath = await exporter.ExportAsync(paths, CreateBundle(paths));

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string[] entryNames = archive.Entries.Select(static entry => entry.FullName).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(entryNames, Does.Not.Contain("tokens.json"));
                Assert.That(entryNames, Does.Not.Contain("sync-app.db"));
                Assert.That(entryNames, Does.Not.Contain("sync-state.db"));
            });
        }

        [Test]
        public async Task ExportAsync_RedactsSecretsFromLogs()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            File.WriteAllText(
                paths.LogFilePath,
                """Authorization: Bearer access-token {"password":"secret","refreshToken":"refresh-token"}""");
            var exporter = new DesktopDiagnosticsExporter();

            string archivePath = await exporter.ExportAsync(paths, CreateBundle(paths));

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string logContent = ReadEntry(archive, "logs/cotton-sync.log");
            Assert.Multiple(() =>
            {
                Assert.That(logContent, Does.Contain("Bearer [redacted]"));
                Assert.That(logContent, Does.Contain("""password":"[redacted]"""));
                Assert.That(logContent, Does.Contain("""refreshToken":"[redacted]"""));
                Assert.That(logContent, Does.Not.Contain("access-token"));
                Assert.That(logContent, Does.Not.Contain("refresh-token"));
                Assert.That(logContent, Does.Not.Contain("secret"));
            });
        }

        [Test]
        public async Task ExportAsync_SerializesSyncPairModeAsReadableString()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var exporter = new DesktopDiagnosticsExporter();

            string archivePath = await exporter.ExportAsync(paths, CreateBundle(paths));

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string diagnosticsJson = ReadEntry(archive, "diagnostics.json");
            using JsonDocument document = JsonDocument.Parse(diagnosticsJson);
            string? mode = document.RootElement
                .GetProperty("syncPairs")[0]
                .GetProperty("mode")
                .GetString();

            Assert.That(mode, Is.EqualTo("fullMirror"));
        }

        [Test]
        public async Task ExportAsync_SerializesDataPathMetadata()
        {
            DesktopAppPaths paths = DesktopAppPaths.CreateForDataDirectory(_tempDirectory);
            var exporter = new DesktopDiagnosticsExporter();

            string archivePath = await exporter.ExportAsync(paths, CreateBundle(paths));

            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            string diagnosticsJson = ReadEntry(archive, "diagnostics.json");
            using JsonDocument document = JsonDocument.Parse(diagnosticsJson);
            JsonElement dataPaths = document.RootElement.GetProperty("dataPaths");

            Assert.Multiple(() =>
            {
                Assert.That(dataPaths.GetProperty("dataDirectory").GetString(), Is.EqualTo(paths.DataDirectory));
                Assert.That(dataPaths.GetProperty("appDatabasePath").GetString(), Is.EqualTo(paths.AppDatabasePath));
                Assert.That(dataPaths.GetProperty("syncStateDatabasePath").GetString(), Is.EqualTo(paths.SyncStateDatabasePath));
                Assert.That(dataPaths.GetProperty("tokenStorePath").GetString(), Is.EqualTo(paths.TokenStorePath));
            });
        }

        private static DesktopDiagnosticsBundle CreateBundle(DesktopAppPaths paths)
        {
            return new DesktopDiagnosticsBundle(
                DateTimeOffset.Parse("2026-06-03T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                "1.0.0",
                "https://app.cottoncloud.dev/",
                "user@example.test",
                new DesktopDataPathSnapshot(
                    paths.DataDirectory,
                    paths.AppDatabasePath,
                    paths.SyncStateDatabasePath,
                    paths.TokenStorePath),
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

        private static string ReadEntry(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException(
                "Diagnostics archive entry is missing: " + entryName);
            using Stream stream = entry.Open();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
