// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Database.Models;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Server.Models.Dto;
using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Cotton.Server.Handlers.Server
{
    public class CreateDatabaseBackupRequest : IRequest<LatestDatabaseBackupDto>
    {
    }

    public class CreateDatabaseBackupRequestHandler(
        IPostgresDumpService _dumper,
        IChunkIngestService _chunkIngest,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        IDatabaseBackupManifestService _manifests,
        DatabaseBackupGate _gate,
        IConfiguration _configuration,
        ILogger<CreateDatabaseBackupRequestHandler> _logger) : IRequestHandler<CreateDatabaseBackupRequest, LatestDatabaseBackupDto>
    {
        public async Task<LatestDatabaseBackupDto> Handle(CreateDatabaseBackupRequest request, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return await CreateBackupAsync(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<LatestDatabaseBackupDto> CreateBackupAsync(CancellationToken ct)
        {
            Stopwatch sw = Stopwatch.StartNew();
            DateTime startedAtUtc = DateTime.UtcNow;
            string backupId = Guid.NewGuid().ToString("N");
            Guid ownerId = await ResolveBackupOwnerIdAsync(ct);
            _logger.LogInformation("Database backup started. BackupId={BackupId}, OwnerId={OwnerId}", backupId, ownerId);

            DumpUploadResult uploadResult = await _dumper.ReadDumpAsync(
                (stream, token) => UploadDumpWithChunkerAsync(stream, ownerId, token), ct);
            if (uploadResult.Chunks.Count == 0)
            {
                throw new InvalidDataException("PostgreSQL produced an empty database dump.");
            }

            BackupManifest manifest = new BackupManifest(
                SchemaVersion: 1,
                BackupId: backupId,
                Elapsed: sw.Elapsed,
                CreatedAtUtc: startedAtUtc,
                Contains: "postgres_database_dump",
                DumpFormat: "pg_dump_custom",
                SourceDatabase: GetConfigOrDefault("DatabaseSettings:Database", "cotton_dev"),
                SourceHost: GetConfigOrDefault("DatabaseSettings:Host", "localhost"),
                SourcePort: GetConfigOrDefault("DatabaseSettings:Port", "5432"),
                HashAlgorithm: Hasher.SupportedHashAlgorithm,
                ChunkSizeBytes: uploadResult.ChunkSizeBytes,
                DumpSizeBytes: uploadResult.DumpSizeBytes,
                DumpContentHash: uploadResult.DumpContentHash,
                ChunkCount: uploadResult.Chunks.Count,
                Chunks: uploadResult.Chunks);

            ResolvedBackupManifest published = await _manifests.PublishAsync(manifest, ct);

            _logger.LogInformation(
                "Database backup completed. BackupId={BackupId}, DumpSizeBytes={DumpSizeBytes}, elapsed: {elapsed}",
                backupId,
                uploadResult.DumpSizeBytes,
                sw.Elapsed.ToString(@"hh\:mm\:ss"));
            return BackupManifestReference.FromManifest(published.ManifestStorageKey, manifest).ToDto(published.Pointer.UpdatedAtUtc);
        }

        private async Task<Guid> ResolveBackupOwnerIdAsync(CancellationToken ct)
        {
            Guid? ownerId = await _dbContext.Users
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct);

            if (ownerId is null || ownerId == Guid.Empty)
            {
                throw new InvalidOperationException("Cannot create backup chunks because no users exist yet.");
            }

            return ownerId.Value;
        }

        private async Task<DumpUploadResult> UploadDumpWithChunkerAsync(Stream dumpStream, Guid ownerId, CancellationToken ct)
        {
            int chunkSize = _settings.GetServerSettings().MaxChunkSizeBytes;
            if (chunkSize <= 0)
            {
                throw new InvalidOperationException("MaxChunkSizeBytes must be positive.");
            }

            using IncrementalHash fileHasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);

            List<BackupChunkInfo> chunks = new List<BackupChunkInfo>();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            long size = 0;
            try
            {
                int order = 0;
                int bytesRead;
                while ((bytesRead = await ReadExactlyAsync(dumpStream, buffer, chunkSize, ct)) > 0)
                {
                    fileHasher.AppendData(buffer, 0, bytesRead);

                    Chunk chunk = await _chunkIngest.UpsertChunkAsync(ownerId, buffer, bytesRead, ct);
                    chunks.Add(new BackupChunkInfo(order, Hasher.ToHexStringHash(chunk.Hash), (int)chunk.PlainSizeBytes));
                    size += bytesRead;
                    order++;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            string fileHashHex = Hasher.ToHexStringHash(fileHasher.GetHashAndReset());
            return new DumpUploadResult(size, chunkSize, fileHashHex, chunks);
        }

        private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            return totalRead;
        }

        private string GetConfigOrDefault(string key, string fallbackValue)
        {
            string? value = _configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallbackValue : value;
        }
    }
}
