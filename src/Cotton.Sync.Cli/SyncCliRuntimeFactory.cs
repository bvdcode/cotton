// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Auth;
using Cotton.Sdk;
using Cotton.Sdk.Auth;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;
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
        CottonCloudClient client = CreateClient(options, httpClient);
        await client.Auth.LoginAsync(
            new LoginRequestDto
            {
                Username = options.Username!,
                Password = options.Password!,
                TwoFactorCode = options.TwoFactorCode,
                TrustDevice = true,
            },
            cancellationToken).ConfigureAwait(false);
        return await CreateAuthenticatedAsync(options, client, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<SyncCliRuntime> CreateWithBrowserAuthAsync(
        SyncCliConnectionOptions options,
        HttpClient httpClient,
        IPlatformCommandService platformCommands,
        CancellationToken cancellationToken)
    {
        CottonCloudClient client = CreateClient(options, httpClient);
        var authFlow = new AppCodeBrowserAuthFlow(client.Auth, platformCommands);
        await authFlow
            .SignInAsync(
                new AppCodeBrowserSignInRequest
                {
                    ApplicationName = "Cotton Sync CLI",
                    DeviceName = "Cotton Sync CLI",
                },
                cancellationToken)
            .ConfigureAwait(false);
        return await CreateAuthenticatedAsync(options, client, cancellationToken).ConfigureAwait(false);
    }

    private static CottonCloudClient CreateClient(SyncCliConnectionOptions options, HttpClient httpClient)
    {
        return new CottonCloudClient(
            httpClient,
            new InMemoryCottonTokenStore(),
            new CottonSdkOptions
            {
                BaseAddress = options.ServerUri,
                RefreshOnUnauthorized = false,
                UserAgent = "CottonSyncCli",
                DeviceName = "Cotton Sync CLI",
            });
    }

    private static async Task<SyncCliRuntime> CreateAuthenticatedAsync(
        SyncCliConnectionOptions options,
        CottonCloudClient client,
        CancellationToken cancellationToken)
    {
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
