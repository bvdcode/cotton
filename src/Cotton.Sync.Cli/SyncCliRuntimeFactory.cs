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

namespace Cotton.Sync.Cli
{
    internal static class SyncCliRuntimeFactory
    {
        public static async Task<SyncCliRuntime> CreateAsync(
            SyncCliConnectionOptions options,
            HttpClient httpClient,
            CancellationToken cancellationToken)
        {
            CottonCloudClient client = CreateClient(options, httpClient);
            bool clientOwnedByRuntime = false;
            try
            {
                await client.Auth.LoginAsync(
                    new LoginRequestDto
                    {
                        Username = options.Username!,
                        Password = options.Password!,
                        TwoFactorCode = options.TwoFactorCode,
                        TrustDevice = true,
                    },
                    cancellationToken).ConfigureAwait(false);
                SyncCliRuntime runtime = await CreateAuthenticatedAsync(options, client, cancellationToken).ConfigureAwait(false);
                clientOwnedByRuntime = true;
                return runtime;
            }
            finally
            {
                if (!clientOwnedByRuntime)
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<SyncCliRuntime> CreateWithBrowserAuthAsync(
            SyncCliConnectionOptions options,
            HttpClient httpClient,
            IPlatformCommandService platformCommands,
            CancellationToken cancellationToken)
        {
            CottonCloudClient client = CreateClient(options, httpClient);
            bool clientOwnedByRuntime = false;
            try
            {
                var authFlow = new AppCodeBrowserAuthFlow(client.Auth, platformCommands);
                await authFlow
                    .SignInAsync(
                        new AppCodeBrowserSignInRequest
                        {
                            ApplicationName = "Cotton Sync CLI",
                            ApplicationVersion = SyncCliAppVersion.Current,
                            DeviceName = "Cotton Sync CLI",
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                SyncCliRuntime runtime = await CreateAuthenticatedAsync(options, client, cancellationToken).ConfigureAwait(false);
                clientOwnedByRuntime = true;
                return runtime;
            }
            finally
            {
                if (!clientOwnedByRuntime)
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
            }
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
            return new SyncCliRuntime(syncPair, stateStore, engine, client);
        }

        public static async Task<SyncCliPassResult> RunSinglePassAsync(
            SyncCliRuntime runtime,
            CancellationToken cancellationToken)
        {
            return await RunSinglePassAsync(runtime, options: null, cancellationToken)
                .ConfigureAwait(false);
        }

        public static async Task<SyncCliPassResult> RunSinglePassAsync(
            SyncCliRuntime runtime,
            SyncRunOptions? options,
            CancellationToken cancellationToken)
        {
            SyncRunResult result = await runtime.Engine
                .RunOnceAsync(runtime.SyncPair, options, cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<SyncStateEntry> entries = await runtime.StateStore
                .LoadPairAsync(runtime.SyncPair.SyncPairId, cancellationToken)
                .ConfigureAwait(false);
            return new SyncCliPassResult(result, entries);
        }
    }
}
