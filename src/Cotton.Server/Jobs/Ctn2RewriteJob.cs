// SPDX-License-Identifier: MIT
// Copyright (c) 2025–2026 Vadim Belov <https://belov.us>

using Cotton.Crypto;
using Cotton.Database;
using Cotton.Database.Integrity;
using Cotton.Database.Models;
using Cotton.Server.Services;
using Cotton.Storage.Abstractions;
using Cotton.Storage.Pipelines;
using EasyExtensions.EntityFrameworkCore.Database;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Quartz;
using System.Text;

namespace Cotton.Server.Jobs
{
    /// <summary>
    /// Rewrites legacy encrypted storage objects and database encrypted values to the current CTN2 format.
    /// </summary>
    [Obsolete("OBSOLETE TRANSITION: CTN2 rewrite job is one-time migration code. Delete this job after all storage objects and encrypted database values are rewritten.")]
    [JobTrigger(days: 1)]
    public class Ctn2RewriteJob(
        IStoragePipeline _storage,
        IStorageBackendProvider _backendProvider,
        CottonDbContext _dbContext,
        IStreamCipher _crypto,
        ILogger<Ctn2RewriteJob> _logger) : IJob
    {
        private const int DatabaseBatchSize = 500;
        private const int ChunkStoredSizeRefreshBatchSize = 1000;
        private const string CompletionMarkerDirectoryName = "cotton";
        private const string CompletionMarkerFilePrefix = "ctn2-integrity-rewrite-";
        private const string StorageCompletionMarkerLogicalKey = "cotton.ctn2-integrity-rewrite.completed.v1";
        private const string CompletionMarkerContent =
            "OBSOLETE TRANSITION: CTN2 and database integrity rewrite completed. Delete this marker with Ctn2RewriteJob after the transition.";
        private static readonly TimeSpan ProgressLogInterval = TimeSpan.FromSeconds(30);
        private static readonly byte[] StorageCompletionMarkerHash = Hasher.HashData(
            Encoding.UTF8.GetBytes(StorageCompletionMarkerLogicalKey));
        private static readonly string StorageCompletionMarkerKey = Hasher.ToHexStringHash(
            StorageCompletionMarkerHash);

        internal static string CompletionStorageMarkerKey => StorageCompletionMarkerKey;

        internal static byte[] CompletionStorageMarkerHash => [.. StorageCompletionMarkerHash];

        /// <summary>
        /// Executes the scheduled CTN2 rewrite pass.
        /// </summary>
        public Task Execute(IJobExecutionContext context)
        {
            return RunOnceAsync(context.CancellationToken);
        }

        internal async Task RunOnceAsync(CancellationToken ct = default)
        {
            string completionMarkerPath = GetCompletionMarkerPath();
            bool storageMarkerExists = await _storage.ExistsAsync(StorageCompletionMarkerKey);
            bool tempMarkerExists = File.Exists(completionMarkerPath);
            if (storageMarkerExists || tempMarkerExists)
            {
                await EnsureCompletionMarkersAsync(completionMarkerPath, storageMarkerExists, tempMarkerExists, ct);
                await ClearStorageMarkerGcScheduleAsync(ct);
                _logger.LogWarning(
                    "OBSOLETE TRANSITION: CTN2 rewrite job skipped because completion marker exists. StorageKey={StorageKey}; TempPath={TempPath}; StorageMarkerExists={StorageMarkerExists}; TempMarkerExists={TempMarkerExists}.",
                    StorageCompletionMarkerKey,
                    completionMarkerPath,
                    storageMarkerExists,
                    tempMarkerExists);
                return;
            }

            var stats = new RewriteStats();

            _logger.LogWarning(
                "OBSOLETE TRANSITION: CTN2 rewrite job started. This job must be deleted after the transition.");

            await RewriteMasterKeySentinelAsync(stats, ct);
            await RewriteStorageObjectsAsync(stats, ct);
            await RewriteEncryptedDatabaseValuesAsync(stats, ct);
            await RewriteDatabaseIntegritySignaturesAsync(stats, ct);
            await WriteCompletionMarkersAsync(completionMarkerPath, ct);

            _logger.LogWarning(
                "OBSOLETE TRANSITION: CTN2 rewrite job finished. " +
                "Storage scanned: {StorageScanned}; storage rewritten: {StorageRewritten}; " +
                "sentinel rewritten: {SentinelRewritten}; database values rewritten: {DatabaseValuesRewritten}; " +
                "database rows re-signed: {DatabaseRowsResigned}; chunk stored sizes refreshed: {ChunkStoredSizesRefreshed}.",
                stats.StorageObjectsScanned,
                stats.StorageObjectsRewritten,
                stats.SentinelRewritten,
                stats.DatabaseValuesRewritten,
                stats.DatabaseRowsResigned,
                stats.ChunkStoredSizesRefreshed);
        }

        private string GetCompletionMarkerPath()
        {
            string markerScope = _dbContext.Database.GetConnectionString()
                ?? AppContext.BaseDirectory;
            string markerHash = Hasher.ToHexStringHash(Hasher.HashData(Encoding.UTF8.GetBytes(markerScope)));
            return Path.Combine(
                Path.GetTempPath(),
                CompletionMarkerDirectoryName,
                CompletionMarkerFilePrefix + markerHash + ".complete");
        }

        internal string GetCompletionMarkerPathForTests() => GetCompletionMarkerPath();

        private async Task EnsureCompletionMarkersAsync(
            string completionMarkerPath,
            bool storageMarkerExists,
            bool tempMarkerExists,
            CancellationToken ct)
        {
            if (!storageMarkerExists)
            {
                await WriteStorageCompletionMarkerAsync(ct);
            }

            if (!tempMarkerExists)
            {
                await WriteTempCompletionMarkerAsync(completionMarkerPath, ct);
            }
        }

        private async Task WriteCompletionMarkersAsync(string completionMarkerPath, CancellationToken ct)
        {
            await WriteStorageCompletionMarkerAsync(ct);
            await WriteTempCompletionMarkerAsync(completionMarkerPath, ct);
            await ClearStorageMarkerGcScheduleAsync(ct);
        }

        private async Task WriteStorageCompletionMarkerAsync(CancellationToken ct)
        {
            byte[] marker = Encoding.UTF8.GetBytes(CompletionMarkerContent);
            await using var stream = new MemoryStream(marker, writable: false);
            await _storage.WriteAsync(
                StorageCompletionMarkerKey,
                stream,
                new PipelineContext
                {
                    FileSizeBytes = stream.Length
                });
        }

        private static async Task WriteTempCompletionMarkerAsync(string completionMarkerPath, CancellationToken ct)
        {
            string? markerDirectory = Path.GetDirectoryName(completionMarkerPath);
            if (!string.IsNullOrWhiteSpace(markerDirectory))
            {
                Directory.CreateDirectory(markerDirectory);
            }

            await File.WriteAllTextAsync(completionMarkerPath, CompletionMarkerContent, ct);
        }

        private async Task ClearStorageMarkerGcScheduleAsync(CancellationToken ct)
        {
            await _dbContext.Chunks
                .Where(c => c.Hash == StorageCompletionMarkerHash && c.GCScheduledAfter != null)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.GCScheduledAfter, (DateTime?)null), ct);
        }

        private async Task RewriteMasterKeySentinelAsync(RewriteStats stats, CancellationToken ct)
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            string storageKey = MasterKeySentinelStore.SentinelStorageKey;
            if (!await backend.ExistsAsync(storageKey))
            {
                return;
            }

            EncryptedObjectFormat format = await ReadEncryptedObjectFormatAsync(backend, storageKey, ct);
            if (format == EncryptedObjectFormat.Current)
            {
                return;
            }

            await using Stream encrypted = await backend.ReadAsync(storageKey);
            await using Stream plaintext = await _crypto.DecryptAsync(encrypted, ct: ct);
            await using Stream rewritten = await _crypto.EncryptAsync(plaintext, ct: ct);
            await backend.WriteAsync(storageKey, rewritten, StorageWriteMode.OverwriteExisting);
            stats.SentinelRewritten = true;

            _logger.LogInformation("Rewrote master-key sentinel {StorageKey} to CTN2.", storageKey);
        }

        private async Task RewriteStorageObjectsAsync(RewriteStats stats, CancellationToken ct)
        {
            IStorageBackend backend = _backendProvider.GetBackend();
            var pendingChunkStoredSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            DateTimeOffset nextProgressLogAt = DateTimeOffset.UtcNow.Add(ProgressLogInterval);

            await foreach (string storageKey in _storage.ListAllKeysAsync(ct))
            {
                ct.ThrowIfCancellationRequested();
                if (string.Equals(
                    storageKey,
                    MasterKeySentinelStore.SentinelStorageKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stats.StorageObjectsScanned++;
                EncryptedObjectFormat format = await ReadEncryptedObjectFormatAsync(backend, storageKey, ct);
                if (format == EncryptedObjectFormat.Current)
                {
                    LogStorageProgressIfNeeded(stats, nextProgressLogAt, out nextProgressLogAt);
                    continue;
                }

                await RewritePipelineObjectAsync(storageKey, ct);
                stats.StorageObjectsRewritten++;
                await QueueChunkStoredSizeRefreshAsync(storageKey, pendingChunkStoredSizes, stats, ct);

                LogStorageProgressIfNeeded(stats, nextProgressLogAt, out nextProgressLogAt);
            }

            await FlushChunkStoredSizeRefreshAsync(pendingChunkStoredSizes, stats, ct);
        }

        private async Task RewritePipelineObjectAsync(string storageKey, CancellationToken ct)
        {
            await using Stream plaintext = await _storage.ReadAsync(storageKey);
            await _storage.WriteAsync(
                storageKey,
                plaintext,
                new PipelineContext(),
                StorageWriteMode.OverwriteExisting);
        }

        private async Task QueueChunkStoredSizeRefreshAsync(
            string storageKey,
            IDictionary<string, long> pendingChunkStoredSizes,
            RewriteStats stats,
            CancellationToken ct)
        {
            if (!TryParseStorageHash(storageKey, out _))
            {
                return;
            }

            pendingChunkStoredSizes[storageKey] = await _storage.GetSizeAsync(storageKey);
            if (pendingChunkStoredSizes.Count >= ChunkStoredSizeRefreshBatchSize)
            {
                await FlushChunkStoredSizeRefreshAsync(pendingChunkStoredSizes, stats, ct);
            }
        }

        private async Task FlushChunkStoredSizeRefreshAsync(
            IDictionary<string, long> pendingChunkStoredSizes,
            RewriteStats stats,
            CancellationToken ct)
        {
            if (pendingChunkStoredSizes.Count == 0)
            {
                return;
            }

            List<byte[]> hashes = [];
            foreach (string storageKey in pendingChunkStoredSizes.Keys)
            {
                if (TryParseStorageHash(storageKey, out byte[] hash))
                {
                    hashes.Add(hash);
                }
            }

            if (hashes.Count == 0)
            {
                pendingChunkStoredSizes.Clear();
                return;
            }

            List<Chunk> chunks = await _dbContext.Chunks
                .Where(c => hashes.Contains(c.Hash))
                .ToListAsync(ct);

            foreach (Chunk chunk in chunks)
            {
                string storageKey = Hasher.ToHexStringHash(chunk.Hash);
                if (!pendingChunkStoredSizes.TryGetValue(storageKey, out long storedSizeBytes))
                {
                    continue;
                }

                if (chunk.StoredSizeBytes == storedSizeBytes)
                {
                    continue;
                }

                chunk.StoredSizeBytes = storedSizeBytes;
                stats.ChunkStoredSizesRefreshed++;
            }

            if (chunks.Count > 0)
            {
                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
            }

            pendingChunkStoredSizes.Clear();
        }

        private async Task RewriteEncryptedDatabaseValuesAsync(RewriteStats stats, CancellationToken ct)
        {
            await RewriteServerSettingsAsync(stats, ct);
            await RewriteOidcProvidersAsync(stats, ct);
            await RewriteOidcLoginStatesAsync(stats, ct);
            await RewriteUsersAsync(stats, ct);
            await RewriteFileManifestsAsync(stats, ct);
        }

        private async Task RewriteDatabaseIntegritySignaturesAsync(RewriteStats stats, CancellationToken ct)
        {
            await RewriteIntegritySignaturesAsync(_dbContext.Users.OrderBy(x => x.Id), nameof(User), stats, ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.UserPasskeyCredentials.OrderBy(x => x.Id),
                nameof(UserPasskeyCredential),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.OidcProviders.OrderBy(x => x.Id),
                nameof(OidcProvider),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.UserExternalIdentities.OrderBy(x => x.Id),
                nameof(UserExternalIdentity),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.OidcLoginStates.OrderBy(x => x.Id),
                nameof(OidcLoginState),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.RefreshTokens.OrderBy(x => x.Id),
                nameof(ExtendedRefreshToken),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.PushDeviceTokens.OrderBy(x => x.Id),
                nameof(PushDeviceToken),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.DownloadTokens.OrderBy(x => x.Id),
                nameof(DownloadToken),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.NodeShareTokens.OrderBy(x => x.Id),
                nameof(NodeShareToken),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.ServerSettings.OrderBy(x => x.Id),
                nameof(CottonServerSettings),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(_dbContext.Nodes.OrderBy(x => x.Id), nameof(Node), stats, ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.NodeFiles.OrderBy(x => x.Id),
                nameof(NodeFile),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.FileManifests.OrderBy(x => x.Id),
                nameof(FileManifest),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(
                _dbContext.FileManifestChunks.OrderBy(x => x.Id),
                nameof(FileManifestChunk),
                stats,
                ct);
            await RewriteIntegritySignaturesAsync(_dbContext.Chunks.OrderBy(x => x.Hash), nameof(Chunk), stats, ct);
        }

        private async Task RewriteIntegritySignaturesAsync<TEntity>(
            IOrderedQueryable<TEntity> orderedQuery,
            string entityName,
            RewriteStats stats,
            CancellationToken ct)
            where TEntity : class
        {
            int offset = 0;
            DateTimeOffset nextProgressLogAt = DateTimeOffset.UtcNow.Add(ProgressLogInterval);
            while (true)
            {
                List<TEntity> batch = await orderedQuery
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                foreach (TEntity entity in batch)
                {
                    QueueIntegritySignatureRewrite(entity);
                }

                await _dbContext.SaveChangesAsync(ct);
                _dbContext.ChangeTracker.Clear();
                offset += batch.Count;
                stats.DatabaseRowsResigned += batch.Count;
                LogIntegrityProgressIfNeeded(entityName, stats, nextProgressLogAt, out nextProgressLogAt);
            }
        }

        private void QueueIntegritySignatureRewrite<TEntity>(TEntity entity)
            where TEntity : class
        {
            EntityEntry<TEntity> entry = _dbContext.Entry(entity);
            if (entry.Metadata.FindProperty(DatabaseIntegrityColumns.VersionProperty) is null
                || entry.Metadata.FindProperty(DatabaseIntegrityColumns.MacProperty) is null)
            {
                throw new InvalidOperationException(
                    $"Cannot re-sign protected entity {typeof(TEntity).Name} because integrity shadow properties are missing.");
            }

            PropertyEntry version = entry.Property(DatabaseIntegrityColumns.VersionProperty);
            PropertyEntry mac = entry.Property(DatabaseIntegrityColumns.MacProperty);
            version.CurrentValue = null;
            mac.CurrentValue = null;
            version.IsModified = true;
            mac.IsModified = true;
        }

        private async Task RewriteServerSettingsAsync(RewriteStats stats, CancellationToken ct)
        {
            int offset = 0;
            while (true)
            {
                List<CottonServerSettings> batch = await _dbContext.ServerSettings
                    .OrderBy(x => x.Id)
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                int rewritten = 0;
                foreach (CottonServerSettings settings in batch)
                {
                    rewritten += RewriteEncryptedStringProperty(
                        settings,
                        nameof(settings.CloudServicesTokenEncrypted),
                        settings.CloudServicesTokenEncrypted);
                    rewritten += RewriteEncryptedStringProperty(
                        settings,
                        nameof(settings.OidcClientSecretEncrypted),
                        settings.OidcClientSecretEncrypted);
                    rewritten += RewriteEncryptedStringProperty(
                        settings,
                        nameof(settings.S3SecretAccessKeyEncrypted),
                        settings.S3SecretAccessKeyEncrypted);
                    rewritten += RewriteEncryptedStringProperty(
                        settings,
                        nameof(settings.SmtpPasswordEncrypted),
                        settings.SmtpPasswordEncrypted);
                    rewritten += RewriteEncryptedStringProperty(
                        settings,
                        nameof(settings.FcmServiceAccountJsonEncrypted),
                        settings.FcmServiceAccountJsonEncrypted);
                }

                await SaveDatabaseRewriteBatchAsync(nameof(CottonServerSettings), rewritten, stats, ct);
                offset += batch.Count;
            }
        }

        private async Task RewriteOidcProvidersAsync(RewriteStats stats, CancellationToken ct)
        {
            int offset = 0;
            while (true)
            {
                List<OidcProvider> batch = await _dbContext.OidcProviders
                    .OrderBy(x => x.Id)
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                int rewritten = 0;
                foreach (OidcProvider provider in batch)
                {
                    rewritten += RewriteEncryptedStringProperty(
                        provider,
                        nameof(provider.ClientSecretEncrypted),
                        provider.ClientSecretEncrypted);
                }

                await SaveDatabaseRewriteBatchAsync(nameof(OidcProvider), rewritten, stats, ct);
                offset += batch.Count;
            }
        }

        private async Task RewriteOidcLoginStatesAsync(RewriteStats stats, CancellationToken ct)
        {
            int offset = 0;
            while (true)
            {
                List<OidcLoginState> batch = await _dbContext.OidcLoginStates
                    .OrderBy(x => x.Id)
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                int rewritten = 0;
                foreach (OidcLoginState loginState in batch)
                {
                    rewritten += RewriteEncryptedStringProperty(
                        loginState,
                        nameof(loginState.CodeVerifierEncrypted),
                        loginState.CodeVerifierEncrypted);
                    rewritten += RewriteEncryptedStringProperty(
                        loginState,
                        nameof(loginState.NonceEncrypted),
                        loginState.NonceEncrypted);
                }

                await SaveDatabaseRewriteBatchAsync(nameof(OidcLoginState), rewritten, stats, ct);
                offset += batch.Count;
            }
        }

        private async Task RewriteUsersAsync(RewriteStats stats, CancellationToken ct)
        {
            int offset = 0;
            while (true)
            {
                List<User> batch = await _dbContext.Users
                    .Where(x => x.TotpSecretEncrypted != null || x.AvatarHashEncrypted != null)
                    .OrderBy(x => x.Id)
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                int rewritten = 0;
                foreach (User user in batch)
                {
                    rewritten += await RewriteEncryptedBytesPropertyAsync(
                        user,
                        nameof(user.TotpSecretEncrypted),
                        user.TotpSecretEncrypted,
                        ct);
                    rewritten += await RewriteEncryptedBytesPropertyAsync(
                        user,
                        nameof(user.AvatarHashEncrypted),
                        user.AvatarHashEncrypted,
                        ct);
                }

                await SaveDatabaseRewriteBatchAsync(nameof(User), rewritten, stats, ct);
                offset += batch.Count;
            }
        }

        private async Task RewriteFileManifestsAsync(RewriteStats stats, CancellationToken ct)
        {
            int offset = 0;
            while (true)
            {
                List<FileManifest> batch = await _dbContext.FileManifests
                    .Where(x => x.SmallFilePreviewHashEncrypted != null)
                    .OrderBy(x => x.Id)
                    .Skip(offset)
                    .Take(DatabaseBatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                int rewritten = 0;
                foreach (FileManifest manifest in batch)
                {
                    rewritten += await RewriteEncryptedBytesPropertyAsync(
                        manifest,
                        nameof(manifest.SmallFilePreviewHashEncrypted),
                        manifest.SmallFilePreviewHashEncrypted,
                        ct);
                }

                await SaveDatabaseRewriteBatchAsync(nameof(FileManifest), rewritten, stats, ct);
                offset += batch.Count;
            }
        }

        private int RewriteEncryptedStringProperty<TEntity>(
            TEntity entity,
            string propertyName,
            string? value)
            where TEntity : class
        {
            if (value is null)
            {
                return 0;
            }

            Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property = _dbContext.Entry(entity)
                .Property(propertyName);
            property.CurrentValue = value;
            property.IsModified = true;
            return 1;
        }

        private async Task<int> RewriteEncryptedBytesPropertyAsync<TEntity>(
            TEntity entity,
            string propertyName,
            byte[]? encryptedValue,
            CancellationToken ct)
            where TEntity : class
        {
            if (encryptedValue is null)
            {
                return 0;
            }

            byte[] plaintext = await _crypto.DecryptAsync(encryptedValue, ct);
            byte[] rewritten = await _crypto.EncryptAsync(plaintext, cancellationToken: ct);

            Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry property = _dbContext.Entry(entity)
                .Property(propertyName);
            property.CurrentValue = rewritten;
            property.IsModified = true;
            return 1;
        }

        private async Task SaveDatabaseRewriteBatchAsync(
            string entityName,
            int rewrittenValues,
            RewriteStats stats,
            CancellationToken ct)
        {
            if (rewrittenValues == 0)
            {
                _dbContext.ChangeTracker.Clear();
                return;
            }

            await _dbContext.SaveChangesAsync(ct);
            _dbContext.ChangeTracker.Clear();
            stats.DatabaseValuesRewritten += rewrittenValues;
            _logger.LogInformation(
                "Rewrote {RewrittenValues} encrypted database values for {EntityName}.",
                rewrittenValues,
                entityName);
        }

        private async Task<EncryptedObjectFormat> ReadEncryptedObjectFormatAsync(
            IStorageBackend backend,
            string storageKey,
            CancellationToken ct)
        {
            await using Stream stream = await backend.ReadAsync(storageKey);
            byte[] magic = new byte[CottonEncryptedStreamFormat.MagicByteLength];
            int read = await ReadAtLeastAsync(stream, magic, ct);
            if (read != magic.Length)
            {
                throw new InvalidDataException(
                    $"Storage object {storageKey} is shorter than the Cotton encrypted stream magic prefix.");
            }

            if (!CottonEncryptedStreamFormat.TryGetVersion(magic, out int version))
            {
                throw new InvalidDataException(
                    $"Storage object {storageKey} does not start with a supported Cotton encrypted stream magic prefix.");
            }

            if (version == CottonEncryptedStreamFormat.CurrentVersion)
            {
                return EncryptedObjectFormat.Current;
            }

            if (version == CottonEncryptedStreamFormat.LegacyVersion)
            {
                return EncryptedObjectFormat.Legacy;
            }

            throw new InvalidDataException(
                $"Storage object {storageKey} uses unsupported Cotton encrypted stream format version {version}.");
        }

        private static async Task<int> ReadAtLeastAsync(Stream stream, byte[] buffer, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(totalRead, buffer.Length - totalRead),
                    ct);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        private static bool TryParseStorageHash(string storageKey, out byte[] hash)
        {
            try
            {
                hash = Hasher.FromHexStringHash(storageKey);
                return true;
            }
            catch (ArgumentException)
            {
                hash = [];
                return false;
            }
        }

        private void LogStorageProgressIfNeeded(
            RewriteStats stats,
            DateTimeOffset nextProgressLogAt,
            out DateTimeOffset nextProgressLog)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < nextProgressLogAt)
            {
                nextProgressLog = nextProgressLogAt;
                return;
            }

            _logger.LogInformation(
                "CTN2 storage rewrite progress: scanned {StorageScanned}, rewritten {StorageRewritten}.",
                stats.StorageObjectsScanned,
                stats.StorageObjectsRewritten);
            nextProgressLog = now.Add(ProgressLogInterval);
        }

        private void LogIntegrityProgressIfNeeded(
            string entityName,
            RewriteStats stats,
            DateTimeOffset nextProgressLogAt,
            out DateTimeOffset nextProgressLog)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < nextProgressLogAt)
            {
                nextProgressLog = nextProgressLogAt;
                return;
            }

            _logger.LogInformation(
                "CTN2 database integrity rewrite progress: re-signed {DatabaseRowsResigned} rows; current entity {EntityName}.",
                stats.DatabaseRowsResigned,
                entityName);
            nextProgressLog = now.Add(ProgressLogInterval);
        }

        private enum EncryptedObjectFormat
        {
            Legacy,
            Current,
        }

        private class RewriteStats
        {
            public long StorageObjectsScanned { get; set; }
            public long StorageObjectsRewritten { get; set; }
            public bool SentinelRewritten { get; set; }
            public long DatabaseValuesRewritten { get; set; }
            public long DatabaseRowsResigned { get; set; }
            public long ChunkStoredSizesRefreshed { get; set; }
        }
    }
}
