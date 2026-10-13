// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using System.Text.Json;

namespace Cotton.Server.Services
{
    public class DatabaseBackupManifestService(
        IStoragePipeline storage,
        DatabaseBackupKeyProvider keyProvider,
        ILogger<DatabaseBackupManifestService> logger) : IDatabaseBackupManifestService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<BackupManifestPointer?> ReadPointerAsync(CancellationToken cancellationToken = default)
        {
            string key = keyProvider.GetScopedPointerStorageKey();
            if (!await storage.ExistsAsync(key))
            {
                return null;
            }

            BackupManifestPointer pointer = await ReadJsonAsync<BackupManifestPointer>(key, false, cancellationToken);
            if (pointer.SchemaVersion is not (1 or 2)
                || pointer.LogicalKey != DatabaseBackupKeyProvider.ManifestPointerLogicalKey)
            {
                throw new InvalidDataException("Unsupported database backup pointer.");
            }

            if (pointer.SchemaVersion == 1)
            {
                BackupManifest manifest = await ReadJsonAsync<BackupManifest>(
                    pointer.LatestManifestStorageKey, true, cancellationToken);
                ValidateManifest(manifest);
                if (manifest.BackupId != pointer.LatestBackupId)
                {
                    throw new InvalidDataException("Database backup pointer does not match its manifest.");
                }
                return pointer with
                {
                    History = [BackupManifestReference.FromManifest(pointer.LatestManifestStorageKey, manifest)]
                };
            }

            if (pointer.History is not { Count: > 0 }
                || pointer.History[0].ManifestStorageKey != pointer.LatestManifestStorageKey
                || pointer.History[0].BackupId != pointer.LatestBackupId
                || pointer.History.Any(backup => string.IsNullOrWhiteSpace(backup.BackupId)
                    || !IsStorageKey(backup.ManifestStorageKey))
                || pointer.History.Select(backup => backup.ManifestStorageKey).Distinct().Count() != pointer.History.Count)
            {
                throw new InvalidDataException("Invalid database backup history.");
            }
            return pointer;
        }

        public async Task<BackupManifest> ReadManifestAsync(
            BackupManifestReference backup, CancellationToken cancellationToken = default)
        {
            BackupManifest manifest = await ReadJsonAsync<BackupManifest>(
                backup.ManifestStorageKey, true, cancellationToken);
            ValidateManifest(manifest);
            if (BackupManifestReference.FromManifest(backup.ManifestStorageKey, manifest) != backup)
            {
                throw new InvalidDataException("Database backup history does not match its manifest.");
            }
            return manifest;
        }

        public async Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default)
        {
            BackupManifestPointer? pointer = await ReadPointerAsync(cancellationToken);
            if (pointer is null)
            {
                return null;
            }
            BackupManifestReference reference = pointer.History![0];
            BackupManifest manifest = await ReadManifestAsync(reference, cancellationToken);
            return new ResolvedBackupManifest(reference.ManifestStorageKey, pointer, manifest);
        }

        public async Task<ResolvedBackupManifest> PublishAsync(
            BackupManifest manifest, CancellationToken cancellationToken = default)
        {
            ValidateManifest(manifest);
            BackupManifestPointer? previous = await ReadPointerAsync(cancellationToken);
            byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            string manifestKey = Hasher.ToHexStringHash(Hasher.HashData(manifestBytes));
            await WriteJsonAsync(manifestKey, manifestBytes, false, cancellationToken);

            DateTime now = DateTime.UtcNow;
            BackupManifestReference reference = BackupManifestReference.FromManifest(manifestKey, manifest);
            BackupManifestReference[] history = DatabaseBackupRetention.Retain(
                new[] { reference }.Concat(previous?.History ?? []), now);
            BackupManifestPointer pointer = new(
                2, DatabaseBackupKeyProvider.ManifestPointerLogicalKey, now,
                history[0].ManifestStorageKey, history[0].BackupId, history);
            byte[] pointerBytes = JsonSerializer.SerializeToUtf8Bytes(pointer, JsonOptions);
            await WriteJsonAsync(keyProvider.GetScopedPointerStorageKey(), pointerBytes, true, cancellationToken);
            logger.LogInformation("Published database backup {BackupId}; retaining {Count} generations.", manifest.BackupId, history.Length);
            return new ResolvedBackupManifest(manifestKey, pointer, manifest);
        }

        private async Task<T> ReadJsonAsync<T>(string key, bool verifyHash, CancellationToken cancellationToken)
        {
            if (!IsStorageKey(key))
            {
                throw new InvalidDataException("Invalid database backup storage key.");
            }
            if (!await storage.ExistsAsync(key))
            {
                throw new FileNotFoundException("Database backup object is missing.", key);
            }
            await using Stream input = await storage.ReadAsync(key);
            using MemoryStream bytes = new();
            await input.CopyToAsync(bytes, cancellationToken);
            byte[] content = bytes.ToArray();
            if (verifyHash && !string.Equals(Hasher.ToHexStringHash(Hasher.HashData(content)), key, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Database backup manifest hash does not match its storage key.");
            }
            try
            {
                return JsonSerializer.Deserialize<T>(content, JsonOptions)
                    ?? throw new InvalidDataException("Database backup object is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Database backup object contains invalid JSON.", exception);
            }
        }

        private async Task WriteJsonAsync(string key, byte[] content, bool overwrite, CancellationToken cancellationToken)
        {
            using MemoryStream stream = new(content, writable: false);
            await storage.WriteAsync(key, stream,
                new PipelineContext { FileSizeBytes = content.Length, Overwrite = overwrite }, cancellationToken);
        }

        private static bool IsStorageKey(string key) =>
            key is { Length: 64 } && key.All(char.IsAsciiHexDigit);

        private static void ValidateManifest(BackupManifest manifest)
        {
            if (manifest.SchemaVersion != 1 || manifest.DumpFormat != "pg_dump_custom"
                || manifest.HashAlgorithm != Hasher.SupportedHashAlgorithm
                || string.IsNullOrWhiteSpace(manifest.BackupId) || manifest.DumpSizeBytes <= 0
                || !IsStorageKey(manifest.DumpContentHash)
                || manifest.Chunks is null || manifest.ChunkCount != manifest.Chunks.Count
                || manifest.ChunkCount == 0
                || manifest.Chunks.Where((chunk, index) => chunk.Order != index
                    || !IsStorageKey(chunk.StorageKey) || chunk.SizeBytes <= 0).Any()
                || manifest.Chunks.Sum(chunk => (long)chunk.SizeBytes) != manifest.DumpSizeBytes)
            {
                throw new InvalidDataException("Invalid database backup manifest.");
            }
        }
    }
}
