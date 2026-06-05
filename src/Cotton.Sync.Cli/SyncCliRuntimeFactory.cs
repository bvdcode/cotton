// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;

namespace Cotton.Sync.Cli;

internal static class SyncCliRuntimeFactory
{
    public static async Task<SyncCliRuntime> CreateAsync(
        SyncCliConnectionOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        var client = new CottonCloudClient(
            httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = options.ServerUri,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncCli",
                DeviceName = "Cotton Sync CLI",
            });
        await client.Auth.LoginAsync(
            new LoginRequestDto
            {
                Username = options.Username,
                Password = options.Password,
                TwoFactorCode = options.TwoFactorCode,
                TrustDevice = true,
            },
            cancellationToken).ConfigureAwait(false);

        var stateStore = new SqliteSyncStateStore(options.DatabasePath);
        var engine = new SyncEngine(
            new LocalFileScanner(),
            new RemoteTreeCrawler(client.Nodes),
            new SdkRemoteFileSynchronizer(client),
            stateStore,
            remoteDirectories: new SdkRemoteDirectorySynchronizer(client.Nodes));
        var syncPair = new SyncPair
        {
            SyncPairId = options.SyncPairId,
            LocalRootPath = options.LocalRoot,
            RemoteRootNodeId = options.RemoteRootNodeId,
        };
        return new SyncCliRuntime(syncPair, stateStore, engine);
    }

    public static async Task<SyncCliPassResult> RunSinglePassAsync(
        SyncCliRuntime runtime,
        CancellationToken cancellationToken)
    {
        SyncRunResult result = await runtime.Engine
            .RunOnceAsync(runtime.SyncPair, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SyncStateEntry> entries = await runtime.StateStore
            .LoadPairAsync(runtime.SyncPair.SyncPairId, cancellationToken)
            .ConfigureAwait(false);
        return new SyncCliPassResult(result, entries);
    }
}
