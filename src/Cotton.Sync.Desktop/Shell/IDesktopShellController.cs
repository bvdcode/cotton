// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.Shell
{
    internal interface IDesktopShellController : IDisposable, IAsyncDisposable
    {
        event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

        event EventHandler<DesktopActivitySnapshot>? ActivityReported;

        event EventHandler<DesktopSessionRevocationSnapshot>? SessionRevoked;

        event EventHandler<DesktopTransferProgressSnapshot>? TransferProgressChanged;

        event EventHandler<DesktopRunProgressSnapshot>? RunProgressChanged;

        Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default);

        Task<DesktopServerProbeResult> ProbeServerAsync(string serverUrl, CancellationToken cancellationToken = default);

        Task<AuthSession> SignInAsync(DesktopSignInRequest request, CancellationToken cancellationToken = default);

        Task<AuthSession> SignInWithBrowserAsync(string serverUrl, CancellationToken cancellationToken = default);

        Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(string remotePath, CancellationToken cancellationToken = default);

        Task<DesktopRemoteFolderSnapshot> CreateRemoteFolderAsync(
            string parentPath,
            string folderName,
            CancellationToken cancellationToken = default);

        Task SignOutAsync(CancellationToken cancellationToken = default);

        Task<SyncPairSettings> AddSyncPairAsync(DesktopSyncPairRequest request, CancellationToken cancellationToken = default);

        Task SetSyncPairEnabledAsync(Guid syncPairId, bool enabled, CancellationToken cancellationToken = default);

        Task SetSyncPairLocalFolderAsync(
            Guid syncPairId,
            string localFolderPath,
            CancellationToken cancellationToken = default);

        Task<SyncPairSettings> SetSyncPairRemoteFolderAsync(
            Guid syncPairId,
            string remoteFolderPath,
            CancellationToken cancellationToken = default);

        Task RenameSyncPairAsync(Guid syncPairId, string displayName, CancellationToken cancellationToken = default);

        Task RemoveSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        Task SyncAllAsync(CancellationToken cancellationToken = default);

        Task PauseAllAsync(CancellationToken cancellationToken = default);

        Task ResumeAllAsync(CancellationToken cancellationToken = default);

        Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default);

        Task OpenWebAsync(CancellationToken cancellationToken = default);

        Task SetStartWithOperatingSystemAsync(bool enabled, CancellationToken cancellationToken = default);

        Task SetNotificationsEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

        Task SetThemeModeAsync(AppThemeMode themeMode, CancellationToken cancellationToken = default);

        Task<DesktopSelfTestSnapshot> RunSelfTestAsync(CancellationToken cancellationToken = default);

        Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default);
    }
}
