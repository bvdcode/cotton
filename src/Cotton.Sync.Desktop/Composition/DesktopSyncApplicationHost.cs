// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.Remote;

namespace Cotton.Sync.Desktop.Composition;

internal sealed class DesktopSyncApplicationHost : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public DesktopSyncApplicationHost(
        ISyncApplicationService app,
        IRemoteRootResolver remoteRootResolver,
        HttpClient httpClient)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
        RemoteRootResolver = remoteRootResolver ?? throw new ArgumentNullException(nameof(remoteRootResolver));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public ISyncApplicationService App { get; }

    public IRemoteRootResolver RemoteRootResolver { get; }

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
