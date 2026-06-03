// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Net.Http.Json;
using Cotton;
using Cotton.Contracts.Nodes;
using Cotton.Models;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Status;
using Cotton.Sync.App.SyncApplication;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Auth;
using Cotton.Sync.Desktop.Composition;
using Cotton.Sync.Desktop.Diagnostics;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Startup;
using Cotton.Sync.State;
using Microsoft.Extensions.Logging;

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopShellController : IDesktopShellController
{
    private static readonly TimeSpan ServerProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly DesktopSyncApplicationFactory _factory;
    private readonly IPlatformCommandService _platformCommands;
    private readonly IAutostartService _autostartService;
    private readonly DesktopDiagnosticsExporter _diagnosticsExporter;
    private readonly DesktopAppPaths _paths;
    private readonly SqliteAppPreferencesStore _preferencesStore;
    private readonly DesktopStartupOptions _startupOptions;
    private readonly SqliteSyncPairSettingsStore _syncPairStore;
    private DesktopSyncApplicationHost? _host;
    private IDisposable? _statusSubscription;

    public DesktopShellController(
        DesktopAppPaths paths,
        DesktopSyncApplicationFactory factory,
        SqliteAppPreferencesStore preferencesStore,
        SqliteSyncPairSettingsStore syncPairStore,
        IPlatformCommandService platformCommands,
        IAutostartService autostartService,
        DesktopStartupOptions? startupOptions = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
        _syncPairStore = syncPairStore ?? throw new ArgumentNullException(nameof(syncPairStore));
        _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
        _autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
        _diagnosticsExporter = new DesktopDiagnosticsExporter();
        _startupOptions = startupOptions ?? DesktopStartupOptions.Empty;
    }

    public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

    public async Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        bool startWithOperatingSystem = await _autostartService
            .IsEnabledAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairStore.ListAsync(cancellationToken).ConfigureAwait(false);
        Uri? serverUrl = _startupOptions.ServerUrl ?? preferences.RememberedServerUrl;
        AuthSession? session = serverUrl is null
            ? null
            : await TryRestoreSessionAsync(serverUrl, cancellationToken).ConfigureAwait(false);
        Uri? signInServerHint = session is not null || _startupOptions.ServerUrl is not null
            ? serverUrl
            : null;
        DesktopPlatformCapabilitySnapshot platformCapabilities = DesktopPlatformCapabilities.CreateSnapshot();
        IReadOnlyList<DesktopSyncPairSnapshot> syncPairSnapshots = await BuildSyncPairSnapshotsAsync(
            syncPairs,
            cancellationToken).ConfigureAwait(false);
        return new DesktopShellSnapshot(
            signInServerHint,
            session?.Email ?? session?.Username,
            _startupOptions.Username ?? preferences.RememberedUsername,
            startWithOperatingSystem,
            preferences.EnableNotifications,
            preferences.ThemeMode,
            platformCapabilities with { IsAutostartSupported = _autostartService.IsSupported },
            session is not null,
            syncPairSnapshots);
    }

    public async Task<DesktopServerProbeResult> ProbeServerAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        Uri parsedServerUrl = ParseServerUrl(serverUrl);
        using var httpClient = new HttpClient
        {
            BaseAddress = parsedServerUrl,
            Timeout = ServerProbeTimeout,
        };
        PublicServerInfo? info = await httpClient
            .GetFromJsonAsync<PublicServerInfo>("/api/v1/server/info", cancellationToken)
            .ConfigureAwait(false);
        bool isCottonServer = string.Equals(info?.Product, Constants.ProductName, StringComparison.Ordinal);
        return new DesktopServerProbeResult(
            parsedServerUrl,
            isCottonServer,
            info?.Product,
            info?.InstanceIdHash);
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
                    TrustDevice = true,
                },
                cancellationToken).ConfigureAwait(false);
            AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
            preferences.RememberedServerUrl = serverUrl;
            preferences.RememberedUsername = request.Username.Trim();
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

    public async Task<DesktopRemoteFolderListSnapshot> ListRemoteFoldersAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        DesktopSyncApplicationHost host = RequireHost();
        string normalizedPath = NormalizeRemotePath(remotePath);
        NodeDto current = await host.Nodes.ResolveAsync(
            normalizedPath == "/" ? null : normalizedPath,
            cancellationToken).ConfigureAwait(false);
        NodeContentDto children = await host.Nodes.GetChildrenAsync(
            current.Id,
            page: 1,
            pageSize: 200,
            depth: 0,
            cancellationToken).ConfigureAwait(false);
        List<DesktopRemoteFolderSnapshot> folders = children.Nodes
            .OrderBy(static node => node.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(node => new DesktopRemoteFolderSnapshot(
                node.Id,
                node.Name,
                CombineRemotePath(normalizedPath, node.Name)))
            .ToList();
        return new DesktopRemoteFolderListSnapshot(normalizedPath, folders);
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

    public async Task OpenWebAsync(CancellationToken cancellationToken = default)
    {
        Uri? serverUrl = _host?.ServerUrl;
        if (serverUrl is null)
        {
            await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
            serverUrl = _startupOptions.ServerUrl ?? preferences.RememberedServerUrl;
        }

        if (serverUrl is null)
        {
            throw new InvalidOperationException("Sign in before opening Cotton Cloud.");
        }

        await _platformCommands.OpenWebAsync(serverUrl, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetStartWithOperatingSystemAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (enabled && !_autostartService.IsSupported)
        {
            throw new NotSupportedException("Autostart is not supported on this platform yet.");
        }

        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _autostartService.SetEnabledAsync(enabled, cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        preferences.StartWithOperatingSystem = enabled;
        preferences.StartMinimizedToTray = enabled && DesktopPlatformCapabilities.IsTrayLifecycleSupported;
        await _preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetNotificationsEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        preferences.EnableNotifications = enabled;
        await _preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetThemeModeAsync(AppThemeMode themeMode, CancellationToken cancellationToken = default)
    {
        ValidateThemeMode(themeMode);
        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        preferences.ThemeMode = themeMode;
        await _preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DesktopSelfTestSnapshot> RunSelfTestAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<DesktopSelfTestItemSnapshot>();
        AppPreferences? preferences = null;
        IReadOnlyList<SyncPairSettings> syncPairs = [];

        await AddSelfTestCheckAsync(
            items,
            "Preferences database",
            async () =>
            {
                await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
                return "Ready";
            }).ConfigureAwait(false);

        await AddSelfTestCheckAsync(
            items,
            "Sync pair database",
            async () =>
            {
                await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                syncPairs = await _syncPairStore.ListAsync(cancellationToken).ConfigureAwait(false);
                return syncPairs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " sync pair(s)";
            }).ConfigureAwait(false);

        await AddSelfTestCheckAsync(
            items,
            "Sync state database",
            async () =>
            {
                var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);
                await stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                return "Ready";
            }).ConfigureAwait(false);

        await AddSelfTestCheckAsync(
            items,
            "Authentication state",
            () => CheckAuthenticationStateAsync(cancellationToken)).ConfigureAwait(false);

        await AddSelfTestCheckAsync(
            items,
            "Autostart adapter",
            async () =>
            {
                bool isEnabled = await _autostartService.IsEnabledAsync(cancellationToken).ConfigureAwait(false);
                return isEnabled ? "Enabled" : "Disabled";
            }).ConfigureAwait(false);

        DesktopPlatformCapabilitySnapshot platformCapabilities = DesktopPlatformCapabilities.CreateSnapshot();
        items.Add(new DesktopSelfTestItemSnapshot(
            "Desktop platform",
            true,
            platformCapabilities.OperatingSystemName
                + "; session: "
                + platformCapabilities.DesktopSession
                + "; desktop: "
                + platformCapabilities.CurrentDesktop));

        items.Add(new DesktopSelfTestItemSnapshot(
            "Tray lifecycle",
            true,
            platformCapabilities.TrayLifecycleDetails));

        IDesktopNotificationService notificationService = DesktopNotificationServiceFactory.CreateDefault();
        items.Add(new DesktopSelfTestItemSnapshot(
            "Notification adapter",
            true,
            notificationService.IsSupported ? "Supported" : "Not available on this platform"));

        await AddSelfTestCheckAsync(
            items,
            "File watcher",
            () => CheckFileWatcherAsync(cancellationToken)).ConfigureAwait(false);

        Uri? serverUrl = _startupOptions.ServerUrl ?? preferences?.RememberedServerUrl;
        if (serverUrl is null)
        {
            items.Add(new DesktopSelfTestItemSnapshot("Server identity", true, "Not configured"));
        }
        else
        {
            await AddSelfTestCheckAsync(
                items,
                "Server identity",
                async () =>
                {
                    DesktopServerProbeResult result = await ProbeServerAsync(
                        serverUrl.AbsoluteUri,
                        cancellationToken).ConfigureAwait(false);
                    if (!result.IsCottonServer)
                    {
                        throw new InvalidOperationException("Cotton server not found.");
                    }

                    return result.Product ?? "Cotton Cloud";
                }).ConfigureAwait(false);
        }

        foreach (SyncPairSettings syncPair in syncPairs)
        {
            await AddSelfTestCheckAsync(
                items,
                "Local root: " + syncPair.DisplayName,
                () => CheckLocalRootAsync(syncPair, cancellationToken)).ConfigureAwait(false);
            DesktopSyncApplicationHost? host = _host;
            if (host is null)
            {
                items.Add(new DesktopSelfTestItemSnapshot(
                    "Remote root: " + syncPair.DisplayName,
                    true,
                    "Sign in to verify"));
            }
            else
            {
                await AddSelfTestCheckAsync(
                    items,
                    "Remote root: " + syncPair.DisplayName,
                    () => CheckRemoteRootAsync(host, syncPair, cancellationToken)).ConfigureAwait(false);
            }
        }

        return new DesktopSelfTestSnapshot(items);
    }

    public async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairStore.ListAsync(cancellationToken).ConfigureAwait(false);
        DesktopSelfTestSnapshot selfTest = await RunSelfTestAsync(cancellationToken).ConfigureAwait(false);
        var bundle = new DesktopDiagnosticsBundle(
            DateTimeOffset.UtcNow,
            typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown",
            (_startupOptions.ServerUrl ?? preferences.RememberedServerUrl)?.AbsoluteUri,
            _host is null ? "Signed out" : preferences.RememberedUsername ?? "Signed in",
            await BuildSyncPairSnapshotsAsync(syncPairs, cancellationToken).ConfigureAwait(false),
            selfTest.Items);
        return await _diagnosticsExporter.ExportAsync(_paths, bundle, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        DesktopSyncApplicationHost? host = _host;
        _host = null;
        _statusSubscription?.Dispose();
        _statusSubscription = null;
        host?.Dispose();
    }

    public static DesktopShellController CreateDefault(DesktopStartupOptions? startupOptions = null)
    {
        return CreateDefault(DesktopAppPaths.CreateDefault(), startupOptions);
    }

    public static DesktopShellController CreateDefault(
        DesktopAppPaths paths,
        DesktopStartupOptions? startupOptions = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var loggerFactory = new DesktopTraceLoggerFactory();
        return new DesktopShellController(
            paths,
            new DesktopSyncApplicationFactory(paths, loggerFactory),
            new SqliteAppPreferencesStore(paths.AppDatabasePath),
            new SqliteSyncPairSettingsStore(paths.AppDatabasePath),
            new ProcessPlatformCommandService(loggerFactory.CreateLogger<ProcessPlatformCommandService>()),
            DesktopAutostartServiceFactory.CreateDefault(),
            startupOptions);
    }

    private static Task<string> CheckLocalRootAsync(
        SyncPairSettings syncPair,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(syncPair.LocalRootPath))
        {
            throw new DirectoryNotFoundException("Local root does not exist: " + syncPair.LocalRootPath);
        }

        _ = Directory.EnumerateFileSystemEntries(syncPair.LocalRootPath).Take(1).ToList();
        return Task.FromResult(syncPair.LocalRootPath);
    }

    private async Task<string> CheckAuthenticationStateAsync(CancellationToken cancellationToken)
    {
        DesktopSyncApplicationHost? host = _host;
        if (host is not null)
        {
            var activeTokens = await host.TokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (activeTokens is null)
            {
                throw new InvalidOperationException("Signed in session has no stored token pair.");
            }

            return "Signed in";
        }

        var tokenStore = new FileCottonTokenStore(_paths.TokenStorePath);
        var storedTokens = await tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
        return storedTokens is null ? "Signed out" : "Stored session available";
    }

    private static Task<string> CheckFileWatcherAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.Combine(Path.GetTempPath(), "cotton-sync-watcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };
            return Task.FromResult("Available");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> CheckRemoteRootAsync(
        DesktopSyncApplicationHost host,
        SyncPairSettings syncPair,
        CancellationToken cancellationToken)
    {
        _ = await host.Nodes.GetAsync(syncPair.RemoteRootNodeId, cancellationToken).ConfigureAwait(false);
        return syncPair.RemoteRootNodeId.ToString();
    }

    private static async Task AddSelfTestCheckAsync(
        List<DesktopSelfTestItemSnapshot> items,
        string name,
        Func<Task<string>> checkAsync)
    {
        try
        {
            string details = await checkAsync().ConfigureAwait(false);
            items.Add(new DesktopSelfTestItemSnapshot(name, true, details));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Trace.TraceWarning("Desktop self-test check failed for {0}: {1}", name, exception);
            items.Add(new DesktopSelfTestItemSnapshot(name, false, exception.Message));
        }
    }

    private async Task<IReadOnlyList<DesktopSyncPairSnapshot>> BuildSyncPairSnapshotsAsync(
        IReadOnlyList<SyncPairSettings> settings,
        CancellationToken cancellationToken)
    {
        if (settings.Count == 0)
        {
            return [];
        }

        var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);
        await stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        SyncAppStatus? currentStatus = _host?.StatusPublisher.Current;
        var snapshots = new List<DesktopSyncPairSnapshot>(settings.Count);
        foreach (SyncPairSettings syncPair in settings)
        {
            string syncPairId = syncPair.Id.ToString();
            IReadOnlyList<SyncStateEntry> entries = await stateStore
                .LoadPairAsync(syncPairId, cancellationToken)
                .ConfigureAwait(false);
            SyncChangeCursor cursor = await stateStore
                .GetChangeCursorAsync(syncPairId, cancellationToken)
                .ConfigureAwait(false);
            SyncPairStatus? status = currentStatus?.SyncPairs
                .FirstOrDefault(pair => pair.SyncPairId == syncPair.Id);
            snapshots.Add(ToSnapshot(syncPair, entries, cursor, status));
        }

        return snapshots;
    }

    private static DesktopSyncPairSnapshot ToSnapshot(
        SyncPairSettings settings,
        IReadOnlyList<SyncStateEntry>? entries = null,
        SyncChangeCursor? cursor = null,
        SyncPairStatus? status = null)
    {
        DateTime? lastSyncedAtUtc = entries is { Count: > 0 }
            ? entries.Max(static entry => entry.SyncedAtUtc)
            : null;
        return new DesktopSyncPairSnapshot(
            settings.Id,
            settings.DisplayName,
            settings.LocalRootPath,
            settings.RemoteDisplayPath,
            status is null ? settings.IsEnabled ? "Idle" : "Disabled" : ToStatusText(status),
            settings.RemoteRootNodeId,
            lastSyncedAtUtc,
            cursor?.LastCursor,
            status?.LastError);
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

    private static string CombineRemotePath(string parentPath, string folderName)
    {
        string normalizedName = NormalizeRequired(folderName, nameof(folderName)).Trim('/');
        return parentPath == "/" ? "/" + normalizedName : parentPath + "/" + normalizedName;
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

    private static void ValidateThemeMode(AppThemeMode themeMode)
    {
        if (!Enum.IsDefined(themeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(themeMode), themeMode, "Unsupported desktop theme mode.");
        }
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
