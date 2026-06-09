// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.App.SyncApplication
{
    /// <summary>
    /// Coordinates high-level sync-client application commands.
    /// </summary>
    public interface ISyncApplicationService
    {
        /// <summary>
        /// Signs in with username/password credentials and optional TOTP.
        /// </summary>
        Task<AuthSession> SignInAsync(PasswordSignInRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs in through browser app-code approval.
        /// </summary>
        Task<AuthSession> SignInWithBrowserAsync(
            AppCodeBrowserSignInRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores a saved authenticated session.
        /// </summary>
        Task<AuthSession> RestoreSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out and stops synchronization.
        /// </summary>
        Task SignOutAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads durable application preferences.
        /// </summary>
        Task<AppPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves durable application preferences.
        /// </summary>
        Task SavePreferencesAsync(AppPreferences preferences, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads configured sync pairs.
        /// </summary>
        Task<IReadOnlyList<SyncPairSettings>> ListSyncPairsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a configured sync pair by identifier.
        /// </summary>
        Task<SyncPairSettings?> GetSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates and persists a sync pair.
        /// </summary>
        Task<SyncPairSaveResult> SaveSyncPairAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a configured sync pair.
        /// </summary>
        Task DeleteSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts configured sync runners.
        /// </summary>
        Task StartSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests one sync pass for every runner.
        /// </summary>
        Task SyncAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests one sync pass for a runner.
        /// </summary>
        Task SyncNowAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses every runner.
        /// </summary>
        Task PauseAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses one runner.
        /// </summary>
        Task PauseAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes every runner.
        /// </summary>
        Task ResumeAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes one runner.
        /// </summary>
        Task ResumeAsync(Guid syncPairId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops all sync runners.
        /// </summary>
        Task StopSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a local folder in the host file manager.
        /// </summary>
        Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a URL in the default browser.
        /// </summary>
        Task OpenWebAsync(Uri url, CancellationToken cancellationToken = default);
    }
}
