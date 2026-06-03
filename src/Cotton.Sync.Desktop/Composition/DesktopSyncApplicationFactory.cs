// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Auth;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;
using HeadlessSyncEngine = Cotton.Sync.SyncEngine;

namespace Cotton.Sync.Desktop.Composition;

internal sealed class DesktopSyncApplicationFactory
{
    private readonly DesktopAppPaths _paths;

    public DesktopSyncApplicationFactory(DesktopAppPaths paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public DesktopSyncApplicationHost Create(Uri serverUrl)
    {
        ArgumentNullException.ThrowIfNull(serverUrl);

        var httpClient = new HttpClient();
        var tokenStore = new FileCottonTokenStore(_paths.TokenStorePath);
        var sdkOptions = new CottonSdkOptions
        {
            BaseAddress = serverUrl,
        };
        var cottonClient = new CottonCloudClient(httpClient, tokenStore, sdkOptions);

        var syncPairStore = new SqliteSyncPairSettingsStore(_paths.AppDatabasePath);
        var preferencesStore = new SqliteAppPreferencesStore(_paths.AppDatabasePath);
        var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);

        var remoteTreeCrawler = new RemoteTreeCrawler(cottonClient.Nodes);
        var remoteFileSynchronizer = new SdkRemoteFileSynchronizer(cottonClient);
        var syncEngine = new HeadlessSyncEngine(
            new LocalFileScanner(),
            remoteTreeCrawler,
            remoteFileSynchronizer,
            stateStore);
        var pairWork = new SyncEnginePairWork(syncEngine);
        var runnerFactory = new SyncPairRunnerFactory(pairWork);
        var statusPublisher = new InMemoryAppStatusPublisher();
        var supervisor = new SyncSupervisor(syncPairStore, runnerFactory, statusPublisher);
        var prerequisites = new SyncPairPrerequisiteValidator(
            new FileSystemLocalSyncRootProbe(),
            new SdkRemoteSyncRootProbe(cottonClient.Nodes));
        var appService = new SyncApplicationService(
            syncPairStore,
            prerequisites,
            preferencesStore,
            new PasswordAuthFlow(cottonClient.Auth),
            supervisor,
            new ProcessPlatformCommandService());
        var remoteRootResolver = new RemoteRootResolver(cottonClient.Nodes);

        return new DesktopSyncApplicationHost(appService, remoteRootResolver, tokenStore, httpClient);
    }
}
