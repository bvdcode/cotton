// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Diagnostics
{
    internal class DesktopDiagnosticsExporter
    {
        private const string DiagnosticsDirectoryName = "diagnostics";
        private const string DiagnosticsJsonEntryName = "diagnostics.json";
        private const string LogEntryPrefix = "logs/";
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        public async Task<string> ExportAsync(
            DesktopAppPaths paths,
            DesktopDiagnosticsBundle bundle,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paths);
            ArgumentNullException.ThrowIfNull(bundle);
            string diagnosticsDirectory = Path.Combine(paths.DataDirectory, DiagnosticsDirectoryName);
            Directory.CreateDirectory(diagnosticsDirectory);
            string archivePath = Path.Combine(diagnosticsDirectory, CreateArchiveFileName(bundle.CreatedAtUtc));

            await using FileStream archiveStream = File.Create(archivePath);
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);
            await WriteJsonEntryAsync(archive, bundle, cancellationToken).ConfigureAwait(false);
            await AddLogEntriesAsync(archive, paths.LogFilePath, cancellationToken).ConfigureAwait(false);
            return archivePath;
        }

        private static async Task WriteJsonEntryAsync(
            ZipArchive archive,
            DesktopDiagnosticsBundle bundle,
            CancellationToken cancellationToken)
        {
            ZipArchiveEntry entry = archive.CreateEntry(DiagnosticsJsonEntryName);
            await using Stream entryStream = entry.Open();
            string json = JsonSerializer.Serialize(bundle, JsonOptions);
            string redactedJson = DesktopSecretRedactor.Redact(json);
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes(redactedJson), cancellationToken).ConfigureAwait(false);
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
            };
            options.Converters.Add(new JsonStringEnumConverter<SyncPairMode>(JsonNamingPolicy.CamelCase));
            return options;
        }

        private static async Task AddLogEntriesAsync(
            ZipArchive archive,
            string logFilePath,
            CancellationToken cancellationToken)
        {
            await AddFileIfExistsAsync(archive, logFilePath, "cotton-sync.log", cancellationToken).ConfigureAwait(false);
            for (int index = 1; index <= 3; index++)
            {
                await AddFileIfExistsAsync(
                    archive,
                    logFilePath + "." + index.ToString(CultureInfo.InvariantCulture),
                    "cotton-sync.log." + index.ToString(CultureInfo.InvariantCulture),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task AddFileIfExistsAsync(
            ZipArchive archive,
            string sourcePath,
            string entryName,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            ZipArchiveEntry entry = archive.CreateEntry(LogEntryPrefix + entryName);
            await using Stream entryStream = entry.Open();
            string logContent;
            await using (var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                using var reader = new StreamReader(sourceStream, Encoding.UTF8);
                logContent = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            string redactedLog = DesktopSecretRedactor.Redact(logContent);
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes(redactedLog), cancellationToken).ConfigureAwait(false);
        }

        private static string CreateArchiveFileName(DateTimeOffset createdAtUtc)
        {
            return "cotton-sync-diagnostics-"
                + createdAtUtc.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-"
                + Guid.NewGuid().ToString("N")[..8]
                + ".zip";
        }
    }
}
