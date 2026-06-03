// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using Cotton.Contracts.Nodes;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Composition;

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopShellController : IDesktopShellController
{
    private static readonly Uri DefaultServerUrl = new("http://localhost:5182");

    private readonly DesktopSyncApplicationFactory _factory;
    private readonly IPlatformCommandService _platformCommands;
    private readonly SqliteAppPreferencesStore _preferencesStore;
    private readonly SqliteSyncPairSettingsStore _syncPairStore;
    private DesktopSyncApplicationHost? _host;
    private IDisposable? _statusSubscription;

    public DesktopShellController(
        DesktopSyncApplicationFactory factory,
        SqliteAppPreferencesStore preferencesStore,
        SqliteSyncPairSettingsStore syncPairStore,
        IPlatformCommandService platformCommands)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _syncPairStore = syncPairStore ?? throw new ArgumentNullException(nameof(syncPairStore));
        _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
    }

    public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

    public async Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairStore.ListAsync(cancellationToken).ConfigureAwait(false);
        Uri serverUrl = preferences.RememberedServerUrl ?? DefaultServerUrl;
        AuthSession? session = await TryRestoreSessionAsync(serverUrl, cancellationToken).ConfigureAwait(false);
        return new DesktopShellSnapshot(
            serverUrl,
            session?.Email ?? session?.Username,
            session is not null,
            syncPairs.Select(ToSnapshot).ToList());
    }

    public async Task<AuthSession> SignInAsync(
        DesktopSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Uri serverUrl = ParseServerUrl(request.ServerUrl);
        DesktopSyncApplicationHost host = _factory.Create(serverUrl);
        try
        {
            AuthSession session = await host.App.SignInAsync(
                new PasswordSignInRequest
                {
                    Username = request.Username.Trim(),
                    Password = request.Password,
                    TwoFactorCode = NormalizeOptional(request.TotpCode),
                    TrustDevice = request.TrustDevice,
                },
                cancellationToken).ConfigureAwait(false);
            var preferences = new AppPreferences
            {
                RememberedServerUrl = serverUrl,
            };
            await host.App.SavePreferencesAsync(preferences, cancellationToken).ConfigureAwait(false);
            await host.App.StartSyncAsync(cancellationToken).ConfigureAwait(false);
            ReplaceHost(host);
            return session;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    public async Task<SyncPairSettings> AddSyncPairAsync(
        DesktopSyncPairRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DesktopSyncApplicationHost host = RequireHost();
        string localPath = NormalizeRequired(request.LocalFolderPath, nameof(request.LocalFolderPath));
        string remotePath = NormalizeRemotePath(request.RemoteFolderPath);
        NodeDto remoteRoot = await host.RemoteRootResolver.EnsureAsync(remotePath, cancellationToken).ConfigureAwait(false);
        var syncPair = new SyncPairSettings
        {
            Id = Guid.NewGuid(),
            DisplayName = CreateDisplayName(localPath, remotePath, remoteRoot),
            LocalRootPath = localPath,
            RemoteRootNodeId = remoteRoot.Id,
            RemoteDisplayPath = remotePath,
            IsEnabled = true,
            Mode = SyncPairMode.FullMirror,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        SyncPairSaveResult result = await host.App.SaveSyncPairAsync(syncPair, cancellationToken).ConfigureAwait(false);
        if (!result.IsSaved)
        {
            throw new SyncPairValidationException(result.Validation.Errors);
        }

        await host.App.StartSyncAsync(cancellationToken).ConfigureAwait(false);
        await host.App.SyncNowAsync(syncPair.Id, cancellationToken).ConfigureAwait(false);
        return syncPair;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        DesktopSyncApplicationHost? host = _host;
        if (host is null)
        {
            return;
        }

        try
        {
            await host.App.SignOutAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_host, host))
            {
                _host = null;
                _statusSubscription?.Dispose();
                _statusSubscription = null;
            }

            host.Dispose();
        }
    }

    public Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        return RequireHost().App.SyncAllAsync(cancellationToken);
    }

    public Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        return RequireHost().App.PauseAllAsync(cancellationToken);
    }

    public Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        return RequireHost().App.ResumeAllAsync(cancellationToken);
    }

    public Task OpenFolderAsync(string localPath, CancellationToken cancellationToken = default)
    {
        return _platformCommands.OpenFolderAsync(localPath, cancellationToken);
    }

    public void Dispose()
    {
        DesktopSyncApplicationHost? host = _host;
        _host = null;
        _statusSubscription?.Dispose();
        _statusSubscription = null;
        host?.Dispose();
    }

    public static DesktopShellController CreateDefault()
    {
        DesktopAppPaths paths = DesktopAppPaths.CreateDefault();
        return new DesktopShellController(
            new DesktopSyncApplicationFactory(paths),
            new SqliteAppPreferencesStore(paths.AppDatabasePath),
            new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
            new ProcessPlatformCommandService());
    }

    private static DesktopSyncPairSnapshot ToSnapshot(SyncPairSettings settings)
    {
        return new DesktopSyncPairSnapshot(
            settings.Id,
            settings.DisplayName,
            settings.LocalRootPath,
            settings.RemoteDisplayPath,
            settings.IsEnabled ? "Idle" : "Disabled");
    }

    private static Uri ParseServerUrl(string serverUrl)
    {
        string normalized = NormalizeRequired(serverUrl, nameof(serverUrl));
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri)
            || !IsHttpScheme(uri))
        {
            throw new ArgumentException("Server URL must be an absolute HTTP or HTTPS URL.", nameof(serverUrl));
        }

        return uri;
    }

    private static bool IsHttpScheme(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRemotePath(string remotePath)
    {
        string normalized = NormalizeRequired(remotePath, nameof(remotePath)).Replace('\\', '/').Trim();
        normalized = normalized.Trim('/');
        return normalized.Length == 0 ? "/" : "/" + normalized;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        string normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string CreateDisplayName(string localPath, string remotePath, NodeDto remoteRoot)
    {
        string localName = Path.GetFileName(localPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(localName))
        {
            return localName;
        }

        if (!string.IsNullOrWhiteSpace(remoteRoot.Name))
        {
            return remoteRoot.Name;
        }

        return remotePath;
    }

    private DesktopSyncApplicationHost RequireHost()
    {
        return _host ?? throw new InvalidOperationException("Sign in before running sync commands.");
    }

    private void ReplaceHost(DesktopSyncApplicationHost host)
    {
        DesktopSyncApplicationHost? previous = _host;
        _statusSubscription?.Dispose();
        _host = host;
        _statusSubscription = host.StatusPublisher.Subscribe(new StatusObserver(this));
        previous?.Dispose();
    }

    private static DesktopSyncStatusSnapshot ToStatusSnapshot(SyncAppStatus status)
    {
        return new DesktopSyncStatusSnapshot(
            status.SyncPairs
                .Select(static syncPair => new DesktopSyncPairStatusSnapshot(
                    syncPair.SyncPairId,
                    ToStatusText(syncPair),
                    syncPair.LastError))
                .ToList());
    }

    private static string ToStatusText(SyncPairStatus status)
    {
        return status.State switch
        {
            SyncPairRunState.Disabled => "Disabled",
            SyncPairRunState.Idle => "Idle",
            SyncPairRunState.Scanning => "Scanning",
            SyncPairRunState.Syncing => "Syncing",
            SyncPairRunState.Paused => "Paused",
            SyncPairRunState.Offline => "Offline",
            SyncPairRunState.Conflict => "Conflict",
            SyncPairRunState.Error => "Error",
            _ => status.State.ToString(),
        };
    }

    private void OnStatusChanged(SyncAppStatus status)
    {
        StatusChanged?.Invoke(this, ToStatusSnapshot(status));
    }

    private async Task<AuthSession?> TryRestoreSessionAsync(
        Uri serverUrl,
        CancellationToken cancellationToken)
    {
        DesktopSyncApplicationHost host = _factory.Create(serverUrl);
        try
        {
            AuthSession session = await host.App.RestoreSessionAsync(cancellationToken).ConfigureAwait(false);
            ReplaceHost(host);
            return session;
        }
        catch (Cotton.Sdk.CottonApiException exception)
        {
            Trace.TraceWarning("Failed to restore desktop session: {0}", exception);
            await host.TokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            host.Dispose();
            return null;
        }
        catch (HttpRequestException)
        {
            Trace.TraceWarning("Failed to restore desktop session because the server is unreachable: {0}", serverUrl);
            host.Dispose();
            return null;
        }
    }

    private sealed class StatusObserver : IObserver<SyncAppStatus>
    {
        private readonly DesktopShellController _controller;

        public StatusObserver(DesktopShellController controller)
        {
            _controller = controller;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            Trace.TraceError(error.ToString());
        }

        public void OnNext(SyncAppStatus value)
        {
            _controller.OnStatusChanged(value);
        }
    }
}
