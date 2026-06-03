// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Diagnostics;

internal sealed class DesktopDiagnosticsExporter
{
    private const string DiagnosticsDirectoryName = "diagnostics";
    private const string DiagnosticsJsonEntryName = "diagnostics.json";
    private const string LogEntryPrefix = "logs/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

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
        await JsonSerializer.SerializeAsync(entryStream, bundle, JsonOptions, cancellationToken).ConfigureAwait(false);
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
        await using FileStream sourceStream = File.OpenRead(sourcePath);
        await sourceStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
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
