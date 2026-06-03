// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.Status;
using Cotton.Sync.Remote;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Nodes;

namespace Cotton.Sync.Desktop.Composition;

internal sealed class DesktopSyncApplicationHost : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public DesktopSyncApplicationHost(
        ISyncApplicationService app,
        IRemoteRootResolver remoteRootResolver,
        IAppStatusPublisher statusPublisher,
        ICottonTokenStore tokenStore,
        ICottonNodeClient nodes,
        HttpClient httpClient,
        Uri serverUrl)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
        RemoteRootResolver = remoteRootResolver ?? throw new ArgumentNullException(nameof(remoteRootResolver));
        StatusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
        TokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ServerUrl = serverUrl ?? throw new ArgumentNullException(nameof(serverUrl));
    }

    public ISyncApplicationService App { get; }

    public IRemoteRootResolver RemoteRootResolver { get; }

    public IAppStatusPublisher StatusPublisher { get; }

    public ICottonTokenStore TokenStore { get; }

    public ICottonNodeClient Nodes { get; }

    public Uri ServerUrl { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}
