// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Storage.Abstractions;
using System.Buffers;
using System.Security.Cryptography;

namespace Cotton.Server.Services
{
    public class DatabaseBackupRestorePreparation(
        IDatabaseBackupManifestService manifests,
        IStoragePipeline storage,
        ILogger<DatabaseBackupRestorePreparation> logger)
    {
        public async Task<PreparedDatabaseBackup> PrepareAsync(
            BackupManifestPointer pointer, string outputPath, CancellationToken cancellationToken)
        {
            List<string> skipped = [];
            foreach (BackupManifestReference reference in pointer.History ?? [])
            {
                try
                {
                    BackupManifest manifest = await manifests.ReadManifestAsync(reference, cancellationToken);
                    await RebuildDumpFileAsync(manifest, outputPath, cancellationToken);
                    return new PreparedDatabaseBackup(
                        new ResolvedBackupManifest(reference.ManifestStorageKey, pointer, manifest), skipped.ToArray());
                }
                catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException
                    or EndOfStreamException or CryptographicException)
                {
                    logger.LogWarning(exception,
                        "Skipping unavailable or invalid database backup {BackupId} created at {CreatedAtUtc}. Trying the preceding generation.",
                        reference.BackupId, reference.CreatedAtUtc);
                    skipped.Add(reference.BackupId);
                }
            }
            throw new InvalidOperationException("None of the retained database backups could be verified. Automatic restore cannot continue.");
        }

        private async Task RebuildDumpFileAsync(BackupManifest manifest, string outputPath, CancellationToken cancellationToken)
        {
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using FileStream output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            using IncrementalHash hasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
            long totalBytes = 0;

            try
            {
                foreach (BackupChunkInfo chunk in manifest.Chunks.OrderBy(x => x.Order))
                {
                    if (!await storage.ExistsAsync(chunk.StorageKey))
                    {
                        throw new FileNotFoundException("Database dump chunk is missing.", chunk.StorageKey);
                    }
                    await using Stream chunkStream = await storage.ReadAsync(chunk.StorageKey);
                    int bytesRead;
                    while ((bytesRead = await chunkStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                    {
                        hasher.AppendData(buffer, 0, bytesRead);
                        await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytes += bytesRead;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            string contentHash = Hasher.ToHexStringHash(hasher.GetHashAndReset());
            if (!string.Equals(contentHash, manifest.DumpContentHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Restored dump hash does not match backup manifest hash.");
            }

            if (totalBytes != manifest.DumpSizeBytes)
            {
                throw new InvalidDataException("Restored dump size does not match backup manifest size.");
            }
        }

    }
}
