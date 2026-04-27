using Cotton.Database;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Server.Providers;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cotton.Server.Jobs
{
    [JobTrigger(days: 7)]
    public class DumpDatabaseJob(
        IPostgresDumpService _dumper,
        IChunkIngestService _chunkIngest,
        IStoragePipeline _storage,
        SettingsProvider _settings,
        CottonDbContext _dbContext,
        DatabaseBackupKeyProvider _backupKeyProvider,
        IConfiguration _configuration,
        ILogger<DumpDatabaseJob> _logger) : IJob
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task Execute(IJobExecutionContext context)
        {
            await Task.Delay(180_000); // Wait for 3 minutes for the server to start up and stabilize

            CancellationToken ct = context.CancellationToken;
            Stopwatch sw = Stopwatch.StartNew();
            DateTime startedAtUtc = DateTime.UtcNow;
            string backupId = Guid.NewGuid().ToString("N");
            string dumpPath = BuildDumpFilePath(startedAtUtc, backupId);

            try
            {
                Guid ownerId = await ResolveBackupOwnerIdAsync(ct);
                _logger.LogInformation("Database dump job started. BackupId={BackupId}, OwnerId={OwnerId}", backupId, ownerId);

                await _dumper.DumpToFileAsync(dumpPath, ct);
                DumpUploadResult uploadResult = await UploadDumpWithChunkerAsync(dumpPath, ownerId, ct);

                var manifest = new BackupManifest(
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

                byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
                string manifestStorageKey = Hasher.ToHexStringHash(Hasher.HashData(manifestBytes));
                await WriteObjectAsync(manifestStorageKey, manifestBytes);

                var pointer = new BackupManifestPointer(
                    SchemaVersion: 1,
                    LogicalKey: DatabaseBackupKeyProvider.ManifestPointerLogicalKey,
                    UpdatedAtUtc: DateTime.UtcNow,
                    LatestManifestStorageKey: manifestStorageKey,
                    LatestBackupId: backupId);

                byte[] pointerBytes = JsonSerializer.SerializeToUtf8Bytes(pointer, JsonOptions);
                string pointerStorageKey = _backupKeyProvider.GetScopedPointerStorageKey();
                await _storage.DeleteAsync(pointerStorageKey);
                await WriteObjectAsync(pointerStorageKey, pointerBytes);

                _logger.LogInformation(
                    "Database dump job completed. BackupId={BackupId}, DumpSizeBytes={DumpSizeBytes}, elapsed: {elapsed}",
                    backupId,
                    uploadResult.DumpSizeBytes,
                    sw.Elapsed.ToString(@"hh\:mm\:ss"));
            }
            finally
            {
                TryDeleteDumpFile(dumpPath);
            }
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

        private async Task<DumpUploadResult> UploadDumpWithChunkerAsync(string dumpPath, Guid ownerId, CancellationToken ct)
        {
            int chunkSize = _settings.GetServerSettings().MaxChunkSizeBytes;
            if (chunkSize <= 0)
            {
                throw new InvalidOperationException("MaxChunkSizeBytes must be positive.");
            }

            await using var dumpStream = new FileStream(dumpPath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, useAsync: true);
            using var fileHasher = IncrementalHash.CreateHash(Hasher.SupportedHashAlgorithmName);

            var chunks = new List<BackupChunkInfo>();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                int order = 0;
                int bytesRead;
                while ((bytesRead = await ReadExactlyAsync(dumpStream, buffer, chunkSize, ct)) > 0)
                {
                    fileHasher.AppendData(buffer, 0, bytesRead);

                    var chunk = await _chunkIngest.UpsertChunkAsync(ownerId, buffer, bytesRead, ct);
                    chunks.Add(new BackupChunkInfo(order, Hasher.ToHexStringHash(chunk.Hash), (int)chunk.PlainSizeBytes));
                    order++;
                }

                if (chunks.Count == 0)
                {
                    var empty = await _chunkIngest.UpsertChunkAsync(ownerId, [], 0, ct);
                    chunks.Add(new BackupChunkInfo(0, Hasher.ToHexStringHash(empty.Hash), 0));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            string fileHashHex = Hasher.ToHexStringHash(fileHasher.GetHashAndReset());
            long size = new FileInfo(dumpPath).Length;
            return new DumpUploadResult(size, chunkSize, fileHashHex, chunks);
        }

        private async Task WriteObjectAsync(string storageKey, byte[] content)
        {
            using var stream = new MemoryStream(content, writable: false);
            await _storage.WriteAsync(storageKey, stream, new PipelineContext
            {
                FileSizeBytes = content.Length
            });
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

        private static string BuildDumpFilePath(DateTime startedAtUtc, string backupId)
        {
            string directory = Path.Combine(Path.GetTempPath(), "cotton", "db-dumps");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"db-{startedAtUtc:yyyyMMdd-HHmmss}-{backupId}.dump");
        }

        private static void TryDeleteDumpFile(string dumpPath)
        {
            try
            {
                if (File.Exists(dumpPath))
                {
                    File.Delete(dumpPath);
                }
            }
            catch
            {
            }
        }

        private string GetConfigOrDefault(string key, string defaultValue)
        {
            string? value = _configuration[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private sealed record DumpUploadResult(
            long DumpSizeBytes,
            int ChunkSizeBytes,
            string DumpContentHash,
            IReadOnlyList<BackupChunkInfo> Chunks);
    }
}
