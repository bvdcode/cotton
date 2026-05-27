// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Storage.Abstractions;

namespace Cotton.Server.Services.KeyManagement;

/// <summary>
/// Startup helpers for keyring v2 runtime wiring.
/// </summary>
internal static class KeyringStartup
{
    public const string EnabledEnvironmentVariable = "COTTON_KEYRING_V2";
    public const string KeyringPathEnvironmentVariable = "COTTON_KEYRING_PATH";
    public const string StoragePathEnvironmentVariable = "COTTON_STORAGE_PATH";

    public static bool IsEnabled()
    {
        string? value = Environment.GetEnvironmentVariable(EnabledEnvironmentVariable);
        return value is not null
            && (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveLocalReplicaRootPath()
    {
        string? explicitPath = Environment.GetEnvironmentVariable(KeyringPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        string? storagePath = Environment.GetEnvironmentVariable(StoragePathEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(storagePath)
            ? storagePath
            : Path.Combine(AppContext.BaseDirectory, "files");
    }

    public static async Task<KeyringBootstrapResult?> BootstrapIfEnabledAsync(
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return null;
        }

        var replica = new KeyringLocalFileReplica(ResolveLocalReplicaRootPath());
        return await BootstrapAsync([replica], legacySettings, unlockSecret, cancellationToken);
    }

    public static async Task<KeyringBootstrapResult?> BootstrapIfEnabledAsync(
        IServiceProvider services,
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!IsEnabled())
        {
            return null;
        }

        List<IKeyringObjectReplica> replicas = [new KeyringLocalFileReplica(ResolveLocalReplicaRootPath())];

        CottonDbContext? dbContext = services.GetService<CottonDbContext>();
        if (dbContext is not null)
        {
            replicas.Add(new KeyringDatabaseReplica(dbContext));
        }

        IStorageBackendProvider? backendProvider = services.GetService<IStorageBackendProvider>();
        if (backendProvider is not null)
        {
            IStorageBackend backend = backendProvider.GetBackend();
            replicas.Add(new KeyringStorageBackendReplica(backend, listByScanning: false));
        }

        return await BootstrapAsync(replicas, legacySettings, unlockSecret, cancellationToken);
    }

    private static async Task<KeyringBootstrapResult> BootstrapAsync(
        IEnumerable<IKeyringObjectReplica> replicas,
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        CancellationToken cancellationToken)
    {
        var store = new KeyringJournaledObjectStore(replicas);
        var bootstrap = new KeyringBootstrapService(store);
        return await bootstrap.OpenOrCreateFromV1Async(
            legacySettings,
            unlockSecret,
            instanceId: null,
            cancellationToken);
    }
}
