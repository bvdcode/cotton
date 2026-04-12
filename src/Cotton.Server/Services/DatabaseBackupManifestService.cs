using System.Text.Json;
using Cotton.Server.Abstractions;
using Cotton.Server.Models.DatabaseBackup;
using Cotton.Storage.Abstractions;

namespace Cotton.Server.Services
{
    public class DatabaseBackupManifestService(
        IStoragePipeline storage,
        DatabaseBackupKeyProvider keyProvider,
        ILogger<DatabaseBackupManifestService> logger) : IDatabaseBackupManifestService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public async Task<ResolvedBackupManifest?> TryGetLatestManifestAsync(CancellationToken cancellationToken = default)
        {
            string pointerStorageKey = keyProvider.GetScopedPointerStorageKey();
            if (!await storage.ExistsAsync(pointerStorageKey))
            {
                return null;
            }

            BackupManifestPointer? pointer = await ReadJsonAsync<BackupManifestPointer>(pointerStorageKey, cancellationToken);
            if (pointer is null || string.IsNullOrWhiteSpace(pointer.LatestManifestStorageKey))
            {
                return null;
            }

            BackupManifest? manifest = await ReadJsonAsync<BackupManifest>(pointer.LatestManifestStorageKey, cancellationToken);
            if (manifest is null)
            {
                logger.LogWarning(
                    "Database backup pointer exists but manifest cannot be loaded. PointerKey={PointerKey}, ManifestKey={ManifestKey}",
                    pointerStorageKey,
                    pointer.LatestManifestStorageKey);
                return null;
            }

            return new ResolvedBackupManifest(pointer.LatestManifestStorageKey, pointer, manifest);
        }


        private async Task<T?> ReadJsonAsync<T>(string storageKey, CancellationToken cancellationToken)
        {
            if (!await storage.ExistsAsync(storageKey))
            {
                return default;
            }

            await using Stream stream = await storage.ReadAsync(storageKey);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        }
    }
}
