// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.Shell;

internal interface IDesktopShellController : IDisposable
{
    Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task<AuthSession> SignInAsync(DesktopSignInRequest request, CancellationToken cancellationToken = default);

    Task<SyncPairSettings> AddSyncPairAsync(DesktopSyncPairRequest request, CancellationToken cancellationToken = default);

    Task SyncAllAsync(CancellationToken cancellationToken = default);

    Task PauseAllAsync(CancellationToken cancellationToken = default);

    Task ResumeAllAsync(CancellationToken cancellationToken = default);

    Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default);
}
