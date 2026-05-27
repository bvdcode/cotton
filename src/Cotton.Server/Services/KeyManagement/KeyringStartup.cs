// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Database;
using Cotton.Storage.Abstractions;
using System.Security.Cryptography;

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

    public static async Task<KeyringStartupOpenResult> TryOpenLocalIfEnabledAsync(
        string unlockSecret,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return KeyringStartupOpenResult.Disabled();
        }

        var replica = new KeyringLocalFileReplica(ResolveLocalReplicaRootPath());
        if (!await HasAnyKeyringObjectAsync(replica, cancellationToken))
        {
            return KeyringStartupOpenResult.NotFound();
        }

        if (string.IsNullOrWhiteSpace(unlockSecret))
        {
            return KeyringStartupOpenResult.Failed("Keyring unlock secret is required.");
        }

        try
        {
            var store = new KeyringJournaledObjectStore([replica]);
            var bootstrap = new KeyringBootstrapService(store);
            KeyringBootstrapResult? keyring = await bootstrap.TryOpenLatestAsync(
                unlockSecret,
                instanceId: null,
                cancellationToken);
            return keyring is null
                ? KeyringStartupOpenResult.Failed("Keyring objects exist, but no valid keyring head could be opened.")
                : KeyringStartupOpenResult.Opened(keyring);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
            or CryptographicException
            or InvalidDataException
            or FormatException
            or IOException)
        {
            return KeyringStartupOpenResult.Failed(ex.Message);
        }
    }

    public static async Task<KeyringBootstrapResult?> BootstrapIfEnabledAsync(
        IServiceProvider services,
        CottonEncryptionSettings legacySettings,
        string unlockSecret,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return null;
        }

        return await BootstrapAsync(CreateReplicas(services), legacySettings, unlockSecret, cancellationToken);
    }

    public static IReadOnlyList<IKeyringObjectReplica> CreateReplicas(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
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

        return replicas;
    }

    private static async Task<bool> HasAnyKeyringObjectAsync(
        IKeyringObjectReplica replica,
        CancellationToken cancellationToken)
    {
        await foreach (string name in replica.ListNamesAsync(cancellationToken))
        {
            if (KeyringObjectNames.IsKeyringObjectName(name))
            {
                return true;
            }
        }

        return false;
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

internal enum KeyringStartupOpenStatus
{
    Disabled,
    NotFound,
    Opened,
    Failed
}

internal sealed record KeyringStartupOpenResult(
    KeyringStartupOpenStatus Status,
    KeyringBootstrapResult? Keyring,
    string? Error)
{
    public static KeyringStartupOpenResult Disabled() => new(
        KeyringStartupOpenStatus.Disabled,
        Keyring: null,
        Error: null);

    public static KeyringStartupOpenResult NotFound() => new(
        KeyringStartupOpenStatus.NotFound,
        Keyring: null,
        Error: null);

    public static KeyringStartupOpenResult Opened(KeyringBootstrapResult keyring) => new(
        KeyringStartupOpenStatus.Opened,
        keyring,
        Error: null);

    public static KeyringStartupOpenResult Failed(string error) => new(
        KeyringStartupOpenStatus.Failed,
        Keyring: null,
        Error: error);
}
