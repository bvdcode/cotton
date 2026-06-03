// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.Status;
using Cotton.Sync.Remote;
using Cotton.Sdk.Auth;

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
        HttpClient httpClient)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
        RemoteRootResolver = remoteRootResolver ?? throw new ArgumentNullException(nameof(remoteRootResolver));
        StatusPublisher = statusPublisher ?? throw new ArgumentNullException(nameof(statusPublisher));
        TokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public ISyncApplicationService App { get; }

    public IRemoteRootResolver RemoteRootResolver { get; }

    public IAppStatusPublisher StatusPublisher { get; }

    public ICottonTokenStore TokenStore { get; }

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
