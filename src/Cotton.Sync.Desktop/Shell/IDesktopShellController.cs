// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.Shell;

internal interface IDesktopShellController : IDisposable
{
    event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

    Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task<DesktopServerProbeResult> ProbeServerAsync(string serverUrl, CancellationToken cancellationToken = default);

    Task<AuthSession> SignInAsync(DesktopSignInRequest request, CancellationToken cancellationToken = default);

    Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(string remotePath, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<SyncPairSettings> AddSyncPairAsync(DesktopSyncPairRequest request, CancellationToken cancellationToken = default);

    Task SyncAllAsync(CancellationToken cancellationToken = default);

    Task PauseAllAsync(CancellationToken cancellationToken = default);

    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default);
}
