// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sdk;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Continuous;
using Cotton.Sync.App.LocalChanges;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Progress;
using Cotton.Sync.App.RemoteChanges;
using Cotton.Sync.App.Runners;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.Supervision;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Auth;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Local;
using Cotton.Sync.Remote;
using Cotton.Sync.State;
using Microsoft.Extensions.Logging;
using HeadlessSyncEngine = Cotton.Sync.SyncEngine;

namespace Cotton.Sync.Desktop.Composition;

internal sealed class DesktopSyncApplicationFactory : IDesktopSyncApplicationFactory
{
    private static readonly TimeSpan HttpRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly ILoggerFactory _loggerFactory;
    private readonly DesktopAppPaths _paths;

    public DesktopSyncApplicationFactory(DesktopAppPaths paths, ILoggerFactory? loggerFactory = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _loggerFactory = loggerFactory ?? new DesktopTraceLoggerFactory();
    }

    public DesktopSyncApplicationHost Create(Uri serverUrl)
    {
        ArgumentNullException.ThrowIfNull(serverUrl);

        var httpClient = new HttpClient
        {
            Timeout = HttpRequestTimeout,
        };
        var tokenStore = new FileCottonTokenStore(_paths.TokenStorePath);
        var sdkOptions = new CottonSdkOptions
        {
            BaseAddress = serverUrl,
            UserAgent = DesktopDeviceIdentity.CreateUserAgent(),
            DeviceName = DesktopDeviceIdentity.CreateDeviceName(),
        };
        var cottonClient = new CottonCloudClient(httpClient, tokenStore, sdkOptions, _loggerFactory);

        var syncPairStore = new SqliteSyncPairSettingsStore(_paths.AppDatabasePath);
        var preferencesStore = new SqliteAppPreferencesStore(_paths.AppDatabasePath);
        var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);

        var remoteTreeCrawler = new RemoteTreeCrawler(cottonClient.Nodes);
        var remoteFileSynchronizer = new SdkRemoteFileSynchronizer(cottonClient);
        var remoteDirectorySynchronizer = new SdkRemoteDirectorySynchronizer(cottonClient.Nodes);
        var remoteChangeFeed = new RemoteChangeFeedReader(cottonClient.Sync, stateStore);
        var syncEngine = new HeadlessSyncEngine(
            new LocalFileScanner(),
            remoteTreeCrawler,
            remoteFileSynchronizer,
            stateStore,
            remoteDirectories: remoteDirectorySynchronizer,
            logger: _loggerFactory.CreateLogger<HeadlessSyncEngine>());
        var activityPublisher = new InMemoryAppActivityPublisher();
        var sessionRevocationPublisher = new InMemorySessionRevocationPublisher();
        var transferProgressPublisher = new InMemoryAppTransferProgressPublisher();
        var runProgressPublisher = new InMemoryAppRunProgressPublisher();
        ISyncPairWork pairWork = new RemoteChangeAwareSyncPairWork(
            new SyncEnginePairWork(syncEngine, activityPublisher, transferProgressPublisher, runProgressPublisher),
            remoteChangeFeed);
        var runnerFactory = new SyncPairRunnerFactory(pairWork, loggerFactory: _loggerFactory);
        var statusPublisher = new InMemoryAppStatusPublisher();
        var supervisor = new SyncSupervisor(syncPairStore, runnerFactory, statusPublisher);
        var localChanges = new LocalChangeSyncCoordinator(
            syncPairStore,
            supervisor,
            new FileSystemLocalSyncRootWatcherFactory(_loggerFactory),
            logger: _loggerFactory.CreateLogger<LocalChangeSyncCoordinator>());
        var periodicSync = new PeriodicSyncCoordinator(
            supervisor,
            logger: _loggerFactory.CreateLogger<PeriodicSyncCoordinator>());
        var platformCommands = new ProcessPlatformCommandService(
            _loggerFactory.CreateLogger<ProcessPlatformCommandService>());
        var authFlow = new PasswordAuthFlow(cottonClient.Auth);
        var appCodeBrowserAuthFlow = new AppCodeBrowserAuthFlow(cottonClient.Auth, platformCommands);
        var sessionRevocationHandler = new SessionRevocationHandler(
            authFlow,
            localChanges,
            periodicSync,
            supervisor,
            sessionRevocationPublisher,
            _loggerFactory.CreateLogger<SessionRevocationHandler>());
        var remoteChanges = new RealtimeRemoteChangeSyncCoordinator(
            cottonClient.Realtime,
            supervisor,
            sessionRevocationHandler: sessionRevocationHandler,
            logger: _loggerFactory.CreateLogger<RealtimeRemoteChangeSyncCoordinator>());
        var prerequisites = new SyncPairPrerequisiteValidator(
            new FileSystemLocalSyncRootProbe(_loggerFactory.CreateLogger<FileSystemLocalSyncRootProbe>()),
            new SdkRemoteSyncRootProbe(
                cottonClient.Nodes,
                _loggerFactory.CreateLogger<SdkRemoteSyncRootProbe>()));
        var appService = new SyncApplicationService(
            syncPairStore,
            prerequisites,
            preferencesStore,
            authFlow,
            appCodeBrowserAuthFlow,
            supervisor,
            platformCommands,
            localChanges,
            remoteChanges,
            periodicSync,
            stateStore,
            logger: _loggerFactory.CreateLogger<SyncApplicationService>());
        var remoteRootResolver = new RemoteRootResolver(cottonClient.Nodes);

        return new DesktopSyncApplicationHost(
            appService,
            remoteRootResolver,
            statusPublisher,
            activityPublisher,
            sessionRevocationPublisher,
            transferProgressPublisher,
            runProgressPublisher,
            tokenStore,
            cottonClient.Nodes,
            cottonClient.Sync,
            httpClient,
            serverUrl,
            cottonClient.Realtime);
    }
}
