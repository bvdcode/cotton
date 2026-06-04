// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.State;
using Microsoft.EntityFrameworkCore;

namespace Cotton.Sync.App.Preferences;

/// <summary>
/// Persists application preferences in a SQLite database through Entity Framework Core.
/// </summary>
public sealed class SqliteAppPreferencesStore : IAppPreferencesStore
{
    private const int PreferencesId = 1;

    private readonly SqliteSyncAppDbContextFactory _contextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteAppPreferencesStore" /> class.
    /// </summary>
    public SqliteAppPreferencesStore(string databasePath)
    {
        _contextFactory = new SqliteSyncAppDbContextFactory(databasePath);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _contextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AppPreferences> GetAsync(CancellationToken cancellationToken = default)
    {
        await using SyncAppDbContext context = _contextFactory.Create();
        AppPreferencesEntity? entity = await context.AppPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == PreferencesId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? new AppPreferences() : ToModel(entity);
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        EnsureSupportedServerUrl(preferences.RememberedServerUrl);
        await using SyncAppDbContext context = _contextFactory.Create();
        AppPreferencesEntity? entity = await context.AppPreferences
            .SingleOrDefaultAsync(item => item.Id == PreferencesId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new AppPreferencesEntity { Id = PreferencesId };
            context.AppPreferences.Add(entity);
        }

        UpdateEntity(entity, preferences);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateEntity(AppPreferencesEntity entity, AppPreferences preferences)
    {
        DateTime now = DateTime.UtcNow;
        DateTime createdAt = preferences.CreatedAtUtc == default
            ? (entity.CreatedAtUtc == default ? now : entity.CreatedAtUtc)
            : preferences.CreatedAtUtc;
        DateTime updatedAt = preferences.UpdatedAtUtc == default ? now : preferences.UpdatedAtUtc;
        entity.RememberedServerUrl = preferences.RememberedServerUrl?.AbsoluteUri;
        entity.RememberedUsername = NormalizeOptional(preferences.RememberedUsername);
        entity.StartWithOperatingSystem = preferences.StartWithOperatingSystem;
        entity.StartMinimizedToTray = preferences.StartMinimizedToTray;
        entity.EnableNotifications = preferences.EnableNotifications;
        entity.ThemeMode = preferences.ThemeMode;
        entity.CreatedAtUtc = UtcDateTime.Normalize(createdAt);
        entity.UpdatedAtUtc = UtcDateTime.Normalize(updatedAt);
    }

    private static AppPreferences ToModel(AppPreferencesEntity entity)
    {
        return new AppPreferences
        {
            RememberedServerUrl = string.IsNullOrWhiteSpace(entity.RememberedServerUrl)
                ? null
                : new Uri(entity.RememberedServerUrl, UriKind.Absolute),
            RememberedUsername = entity.RememberedUsername,
            StartWithOperatingSystem = entity.StartWithOperatingSystem,
            StartMinimizedToTray = entity.StartMinimizedToTray,
            EnableNotifications = entity.EnableNotifications,
            ThemeMode = entity.ThemeMode,
            CreatedAtUtc = UtcDateTime.Normalize(entity.CreatedAtUtc),
            UpdatedAtUtc = UtcDateTime.Normalize(entity.UpdatedAtUtc),
        };
    }

    private static void EnsureSupportedServerUrl(Uri? serverUrl)
    {
        if (serverUrl is null)
        {
            return;
        }

        if (!serverUrl.IsAbsoluteUri || !IsHttpScheme(serverUrl))
        {
            throw new ArgumentException(
                "Remembered server URL must be an absolute HTTP or HTTPS URL.",
                nameof(serverUrl));
        }
    }

    private static bool IsHttpScheme(Uri serverUrl)
    {
        return string.Equals(serverUrl.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(serverUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
