// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Activities;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.Status;
using Cotton.Sync.Remote;
using Cotton.Sdk.Auth;
using Cotton.Sdk.Nodes;
using Cotton.Sdk.Sync;

namespace Cotton.Sync.Desktop.Composition;

internal sealed class DesktopSyncApplicationHost : IDisposable, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncDisposable? _asyncResource;
    private bool _disposed;

    public DesktopSyncApplicationHost(
        ISyncApplicationService app,
        IRemoteRootResolver remoteRootResolver,
        IAppStatusPublisher statusPublisher,
        IAppActivityPublisher activityPublisher,
        ICottonTokenStore tokenStore,
        ICottonNodeClient nodes,
        ICottonSyncClient sync,
        HttpClient httpClient,
        Uri serverUrl,
        IAsyncDisposable? asyncResource = null)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
        RemoteRootResolver = remoteRootResolver ?? throw new ArgumentNullException(nameof(remoteRootResolver));
        StatusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
        ActivityPublisher = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
        TokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        Sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _asyncResource = asyncResource;
        ServerUrl = serverUrl ?? throw new ArgumentNullException(nameof(serverUrl));
    }

    public ISyncApplicationService App { get; }

    public IRemoteRootResolver RemoteRootResolver { get; }

    public IAppStatusPublisher StatusPublisher { get; }

    public IAppActivityPublisher ActivityPublisher { get; }

    public ICottonTokenStore TokenStore { get; }

    public ICottonNodeClient Nodes { get; }

    public ICottonSyncClient Sync { get; }

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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_asyncResource is not null)
        {
            await _asyncResource.DisposeAsync().ConfigureAwait(false);
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}
