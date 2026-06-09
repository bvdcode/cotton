// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.App.Preferences
{
    /// <summary>
    /// Persists durable desktop sync-client preferences.
    /// </summary>
    public interface IAppPreferencesStore
    {
        /// <summary>
        /// Initializes the backing store.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads current application preferences.
        /// </summary>
        Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves current application preferences.
        /// </summary>
        Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default);
    }
}
