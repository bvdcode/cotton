// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Cotton;
using Cotton.Nodes;
using Cotton.Models;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Activities;
using Cotton.Sync.App.Platform;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.Progress;
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
using AppRunProgress = Cotton.Sync.App.Progress.AppRunProgress;
using AppTransferProgress = Cotton.Sync.App.Progress.AppTransferProgress;

namespace Cotton.Sync.Desktop.Shell
{

    internal sealed class DesktopShellController : IDesktopShellController
    {
        private const string SelfTestSyncPairId = "__desktop_self_test__";

        private static readonly TimeSpan SavedSessionRestoreTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ServerProbeTimeout = TimeSpan.FromSeconds(5);

        private readonly IDesktopSyncApplicationFactory _factory;
        private readonly IPlatformCommandService _platformCommands;
        private readonly IAutostartService _autostartService;
        private readonly DesktopDiagnosticsExporter _diagnosticsExporter;
        private readonly Func<CancellationToken, Task<DesktopTokenStorageCapabilitySnapshot>> _tokenStorageVerifier;
        private readonly DesktopAppPaths _paths;
        private readonly SqliteAppPreferencesStore _preferencesStore;
        private readonly DesktopStartupOptions _startupOptions;
        private readonly TimeSpan _savedSessionRestoreTimeout;
        private readonly SqliteSyncPairSettingsStore _syncPairStore;
        private readonly TimeSpan _tokenStorageVerificationTimeout;
        private IDisposable? _activitySubscription;
        private DesktopSyncApplicationHost? _host;
        private IDisposable? _runProgressSubscription;
        private IDisposable? _sessionRevocationSubscription;
        private IDisposable? _statusSubscription;
        private IDisposable? _transferProgressSubscription;

        public DesktopShellController(
            DesktopAppPaths paths,
            IDesktopSyncApplicationFactory factory,
            SqliteAppPreferencesStore preferencesStore,
            SqliteSyncPairSettingsStore syncPairStore,
            IPlatformCommandService platformCommands,
            IAutostartService autostartService,
            DesktopStartupOptions? startupOptions = null,
            Func<DesktopTokenStorageCapabilitySnapshot>? tokenStorageCapabilities = null,
            Func<CancellationToken, Task<DesktopTokenStorageCapabilitySnapshot>>? tokenStorageVerifier = null,
            TimeSpan? savedSessionRestoreTimeout = null,
            TimeSpan? tokenStorageVerificationTimeout = null)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _preferencesStore = preferencesStore ?? throw new ArgumentNullException(nameof(preferencesStore));
            _syncPairStore = syncPairStore ?? throw new ArgumentNullException(nameof(syncPairStore));
            _platformCommands = platformCommands ?? throw new ArgumentNullException(nameof(platformCommands));
            _autostartService = autostartService ?? throw new ArgumentNullException(nameof(autostartService));
            _diagnosticsExporter = new DesktopDiagnosticsExporter();
            _startupOptions = startupOptions ?? DesktopStartupOptions.Empty;
            _savedSessionRestoreTimeout = savedSessionRestoreTimeout ?? SavedSessionRestoreTimeout;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_savedSessionRestoreTimeout, TimeSpan.Zero);
            _tokenStorageVerificationTimeout = tokenStorageVerificationTimeout ?? _savedSessionRestoreTimeout;
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_tokenStorageVerificationTimeout, TimeSpan.Zero);
            _tokenStorageVerifier = tokenStorageVerifier
                ?? (tokenStorageCapabilities is null
                    ? DesktopTokenStorageCapabilities.CreateVerifiedSnapshotAsync
                    : cancellationToken => Task.FromResult(tokenStorageCapabilities()));
        }

        public event EventHandler<DesktopSyncStatusSnapshot>? StatusChanged;

        public event EventHandler<DesktopActivitySnapshot>? ActivityReported;

        public event EventHandler<DesktopSessionRevocationSnapshot>? SessionRevoked;

        public event EventHandler<DesktopTransferProgressSnapshot>? TransferProgressChanged;

        public event EventHandler<DesktopRunProgressSnapshot>? RunProgressChanged;

        public async Task<DesktopShellSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await InitializeSyncStateStoreAsync(cancellationToken).ConfigureAwait(false);
            AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
            bool startWithOperatingSystem = await ResolveStartWithOperatingSystemAsync(
                preferences,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<SyncPairSettings> syncPairs = await _syncPairStore.ListAsync(cancellationToken).ConfigureAwait(false);
            Uri? serverUrl = _startupOptions.ServerUrl ?? preferences.RememberedServerUrl;
            string? startupErrorMessage = null;
            AuthSession? session = null;
            if (serverUrl is not null)
            {
                try
                {
                    session = await TryRestoreSessionAsync(serverUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Trace.TraceWarning("Failed to restore desktop session for {0}: {1}", serverUrl, exception);
                    startupErrorMessage = DesktopActionRequiredMessageResolver.FromException(exception);
                }
            }

            if (session is not null)
            {
                bool applied = await TryApplyPreferredAutostartAsync(preferences, cancellationToken).ConfigureAwait(false);
                if (applied)
                {
                    startWithOperatingSystem = true;
                    await _preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
                }
            }

            DesktopPlatformCapabilitySnapshot platformCapabilities = DesktopPlatformCapabilities.CreateSnapshot();
            IReadOnlyList<DesktopSyncPairSnapshot> syncPairSnapshots = await BuildSyncPairSnapshotsAsync(
                syncPairs,
                cancellationToken).ConfigureAwait(false);
            return new DesktopShellSnapshot(
                serverUrl,
                session?.Email ?? session?.Username,
                _startupOptions.Username ?? preferences.RememberedUsername,
                startWithOperatingSystem,
                preferences.EnableNotifications,
                preferences.ThemeMode,
                CreateDataPathSnapshot(),
                platformCapabilities with { IsAutostartSupported = _autostartService.IsSupported },
                session is not null,
                syncPairSnapshots,
                DesktopDeviceIdentity.CreateDeviceName(),
                startupErrorMessage);
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
            await EnsureReleaseSecureTokenStorageAsync(cancellationToken).ConfigureAwait(false);
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
                await CompleteSignInAsync(host, serverUrl, session, request.Username.Trim(), cancellationToken)
                    .ConfigureAwait(false);
                return session;
            }
            catch
            {
                await host.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<AuthSession> SignInWithBrowserAsync(
            string serverUrl,
            CancellationToken cancellationToken = default)
        {
            Uri parsedServerUrl = ParseServerUrl(serverUrl);
            await EnsureReleaseSecureTokenStorageAsync(cancellationToken).ConfigureAwait(false);
            DesktopSyncApplicationHost host = _factory.Create(parsedServerUrl);
            try
            {
                AuthSession session = await host.App.SignInWithBrowserAsync(
                    new AppCodeBrowserSignInRequest
                    {
                        ApplicationName = "Cotton Sync Desktop",
                        ApplicationVersion = DesktopAppVersion.Current,
                        DeviceName = DesktopDeviceIdentity.CreateDeviceName(),
                    },
                    cancellationToken).ConfigureAwait(false);
                await CompleteSignInAsync(
                        host,
                        parsedServerUrl,
                        session,
                        session.Email ?? session.Username,
                        cancellationToken)
                    .ConfigureAwait(false);
                return session;
            }
            catch
            {
                await host.DisposeAsync().ConfigureAwait(false);
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

        public async Task<DesktopRemoteFolderSnapshot> CreateRemoteFolderAsync(
            string parentPath,
            string folderName,
            CancellationToken cancellationToken = default)
        {
            DesktopSyncApplicationHost host = RequireHost();
            string normalizedPath = NormalizeRemotePath(parentPath);
            string normalizedName = NormalizeRequired(folderName, nameof(folderName));
            if (normalizedName.Contains('/') || normalizedName.Contains('\\'))
            {
                throw new ArgumentException("Cloud folder name cannot contain path separators.", nameof(folderName));
            }

            NodeDto parent = await host.Nodes.ResolveAsync(
                normalizedPath == "/" ? null : normalizedPath,
                cancellationToken).ConfigureAwait(false);
            NodeDto created = await host.Nodes.CreateAsync(parent.Id, normalizedName, cancellationToken)
                .ConfigureAwait(false);
            return new DesktopRemoteFolderSnapshot(
                created.Id,
                created.Name,
                CombineRemotePath(normalizedPath, created.Name));
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

            try
            {
                await host.App.SyncNowAsync(syncPair.Id, cancellationToken).ConfigureAwait(false);
                return syncPair;
            }
            catch (Exception)
            {
                await RollBackAddedSyncPairAsync(host, syncPair.Id, cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task SetSyncPairEnabledAsync(
            Guid syncPairId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            if (syncPairId == Guid.Empty)
            {
                throw new ArgumentException("Sync pair id is required.", nameof(syncPairId));
            }

            await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            SyncPairSettings? syncPair = await _syncPairStore.GetAsync(syncPairId, cancellationToken).ConfigureAwait(false);
            if (syncPair is null)
            {
                throw new InvalidOperationException("Sync pair was not found.");
            }

            if (syncPair.IsEnabled == enabled)
            {
                return;
            }

            syncPair.IsEnabled = enabled;
            syncPair.UpdatedAtUtc = DateTime.UtcNow;
            await SaveSyncPairSettingsAsync(syncPair, cancellationToken).ConfigureAwait(false);
        }

        public async Task RenameSyncPairAsync(
            Guid syncPairId,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            if (syncPairId == Guid.Empty)
            {
                throw new ArgumentException("Sync pair id is required.", nameof(syncPairId));
            }

            string normalizedDisplayName = NormalizeRequired(displayName, nameof(displayName));
            await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            SyncPairSettings? syncPair = await _syncPairStore.GetAsync(syncPairId, cancellationToken).ConfigureAwait(false);
            if (syncPair is null)
            {
                throw new InvalidOperationException("Sync pair was not found.");
            }

            if (string.Equals(syncPair.DisplayName, normalizedDisplayName, StringComparison.Ordinal))
            {
                return;
            }

            syncPair.DisplayName = normalizedDisplayName;
            syncPair.UpdatedAtUtc = DateTime.UtcNow;
            await SaveSyncPairSettingsAsync(syncPair, cancellationToken).ConfigureAwait(false);
        }

        public async Task SetSyncPairLocalFolderAsync(
            Guid syncPairId,
            string localFolderPath,
            CancellationToken cancellationToken = default)
        {
            if (syncPairId == Guid.Empty)
            {
                throw new ArgumentException("Sync pair id is required.", nameof(syncPairId));
            }

            string normalizedLocalPath = NormalizeRequired(localFolderPath, nameof(localFolderPath));
            await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            SyncPairSettings? syncPair = await _syncPairStore.GetAsync(syncPairId, cancellationToken).ConfigureAwait(false);
            if (syncPair is null)
            {
                throw new InvalidOperationException("Sync pair was not found.");
            }

            if (string.Equals(syncPair.LocalRootPath, normalizedLocalPath, StringComparison.Ordinal))
            {
                return;
            }

            syncPair.LocalRootPath = normalizedLocalPath;
            syncPair.UpdatedAtUtc = DateTime.UtcNow;
            await SaveSyncPairSettingsAsync(syncPair, cancellationToken).ConfigureAwait(false);
        }

        public async Task<SyncPairSettings> SetSyncPairRemoteFolderAsync(
            Guid syncPairId,
            string remoteFolderPath,
            CancellationToken cancellationToken = default)
        {
            if (syncPairId == Guid.Empty)
            {
                throw new ArgumentException("Sync pair id is required.", nameof(syncPairId));
            }

            DesktopSyncApplicationHost host = RequireHost();
            string normalizedRemotePath = NormalizeRemotePath(remoteFolderPath);
            await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            SyncPairSettings? syncPair = await _syncPairStore.GetAsync(syncPairId, cancellationToken).ConfigureAwait(false);
            if (syncPair is null)
            {
                throw new InvalidOperationException("Sync pair was not found.");
            }

            if (string.Equals(syncPair.RemoteDisplayPath, normalizedRemotePath, StringComparison.Ordinal))
            {
                return syncPair;
            }

            NodeDto remoteRoot = await host.RemoteRootResolver
                .EnsureAsync(normalizedRemotePath, cancellationToken)
                .ConfigureAwait(false);
            syncPair.RemoteRootNodeId = remoteRoot.Id;
            syncPair.RemoteDisplayPath = normalizedRemotePath;
            syncPair.UpdatedAtUtc = DateTime.UtcNow;
            await SaveSyncPairSettingsAsync(syncPair, cancellationToken).ConfigureAwait(false);
            return syncPair;
        }

        public async Task RemoveSyncPairAsync(Guid syncPairId, CancellationToken cancellationToken = default)
        {
            if (syncPairId == Guid.Empty)
            {
                throw new ArgumentException("Sync pair id is required.", nameof(syncPairId));
            }

            DesktopSyncApplicationHost? host = _host;
            if (host is null)
            {
                await _syncPairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await _syncPairStore.DeleteAsync(syncPairId, cancellationToken).ConfigureAwait(false);
                var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);
                await stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await stateStore.DeletePairAsync(syncPairId.ToString(), cancellationToken).ConfigureAwait(false);
                return;
            }

            await host.App.DeleteSyncPairAsync(syncPairId, cancellationToken).ConfigureAwait(false);
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
                    _activitySubscription?.Dispose();
                    _activitySubscription = null;
                    _statusSubscription?.Dispose();
                    _statusSubscription = null;
                    _transferProgressSubscription?.Dispose();
                    _transferProgressSubscription = null;
                    _runProgressSubscription?.Dispose();
                    _runProgressSubscription = null;
                }

                await host.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task CompleteSignInAsync(
            DesktopSyncApplicationHost host,
            Uri serverUrl,
            AuthSession session,
            string rememberedUsername,
            CancellationToken cancellationToken)
        {
            await _preferencesStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
            AppPreferences preferences = await _preferencesStore.GetAsync(cancellationToken).ConfigureAwait(false);
            preferences.RememberedServerUrl = serverUrl;
            preferences.RememberedUsername = rememberedUsername;
            await TryApplyPreferredAutostartAsync(preferences, cancellationToken).ConfigureAwait(false);
            await host.App.SavePreferencesAsync(preferences, cancellationToken).ConfigureAwait(false);
            await host.App.StartSyncAsync(cancellationToken).ConfigureAwait(false);
            await ReplaceHostAsync(host, cancellationToken).ConfigureAwait(false);
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

        private async Task<bool> ResolveStartWithOperatingSystemAsync(
            AppPreferences preferences,
            CancellationToken cancellationToken)
        {
            bool isEnabled = await TryReadStartWithOperatingSystemAsync(cancellationToken).ConfigureAwait(false);
            if (isEnabled)
            {
                return true;
            }

            return preferences.StartWithOperatingSystem && _autostartService.IsSupported;
        }

        private async Task<bool> TryReadStartWithOperatingSystemAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _autostartService.IsEnabledAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Trace.TraceError("Failed to read Cotton Sync autostart state. {0}", exception);
                return false;
            }
        }

        private async Task<bool> TryApplyPreferredAutostartAsync(
            AppPreferences preferences,
            CancellationToken cancellationToken)
        {
            if (!preferences.StartWithOperatingSystem || !_autostartService.IsSupported)
            {
                return false;
            }

            try
            {
                bool isEnabled = await _autostartService.IsEnabledAsync(cancellationToken).ConfigureAwait(false);
                if (!isEnabled)
                {
                    await _autostartService.SetEnabledAsync(true, cancellationToken).ConfigureAwait(false);
                }

                preferences.StartMinimizedToTray = DesktopPlatformCapabilities.IsTrayLifecycleSupported;
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Trace.TraceError("Failed to apply Cotton Sync autostart preference. {0}", exception);
                return false;
            }
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
                    await stateStore.GetChangeCursorAsync(SelfTestSyncPairId, cancellationToken).ConfigureAwait(false);
                    return "Ready: " + _paths.SyncStateDatabasePath;
                }).ConfigureAwait(false);

            await AddSelfTestCheckAsync(
                items,
                "Authentication state",
                () => CheckAuthenticationStateAsync(cancellationToken)).ConfigureAwait(false);

            DesktopTokenStorageCapabilitySnapshot tokenStorage = await _tokenStorageVerifier(cancellationToken)
                .ConfigureAwait(false);
            items.Add(new DesktopSelfTestItemSnapshot(
                "Token storage",
                tokenStorage.IsReleaseSecure,
                tokenStorage.IsReleaseSecure
                    ? tokenStorage.Details
                    : tokenStorage.Details + " (not release secure)"));

            await AddSelfTestCheckAsync(
                items,
                "Desktop icon",
                () => CheckDesktopIconAsync(cancellationToken)).ConfigureAwait(false);

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

            DesktopNotificationCapabilitySnapshot notificationCapabilities =
                DesktopNotificationServiceFactory.CreateCapabilitySnapshot();
            items.Add(new DesktopSelfTestItemSnapshot(
                "Notification adapter",
                true,
                notificationCapabilities.Details));

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

            DesktopSyncApplicationHost? activeHost = _host;
            if (serverUrl is null)
            {
                items.Add(new DesktopSelfTestItemSnapshot("Desktop sync change feed", true, "Not configured"));
            }
            else if (activeHost is null)
            {
                items.Add(new DesktopSelfTestItemSnapshot("Desktop sync change feed", true, "Sign in to verify"));
            }
            else
            {
                await AddSelfTestCheckAsync(
                    items,
                    "Desktop sync change feed",
                    () => CheckSyncChangeFeedAsync(activeHost, cancellationToken)).ConfigureAwait(false);
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
                DesktopAppVersion.Current,
                (_startupOptions.ServerUrl ?? preferences.RememberedServerUrl)?.AbsoluteUri,
                _host is null ? "Signed out" : preferences.RememberedUsername ?? "Signed in",
                CreateDataPathSnapshot(),
                await BuildSyncPairSnapshotsAsync(syncPairs, cancellationToken).ConfigureAwait(false),
                selfTest.Items);
            return await _diagnosticsExporter.ExportAsync(_paths, bundle, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            DesktopSyncApplicationHost? host = DetachHost();
            if (host is not null)
            {
                StopAndDisposeHostAsync(host).GetAwaiter().GetResult();
            }
        }

        public async ValueTask DisposeAsync()
        {
            DesktopSyncApplicationHost? host = DetachHost();
            if (host is not null)
            {
                await StopAndDisposeHostAsync(host).ConfigureAwait(false);
            }
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

        private async Task InitializeSyncStateStoreAsync(CancellationToken cancellationToken)
        {
            var stateStore = new SqliteSyncStateStore(_paths.SyncStateDatabasePath);
            await stateStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
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

        private DesktopDataPathSnapshot CreateDataPathSnapshot()
        {
            return new DesktopDataPathSnapshot(
                _paths.DataDirectory,
                _paths.AppDatabasePath,
                _paths.SyncStateDatabasePath,
                _paths.TokenStorePath);
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

        private static Task<string> CheckDesktopIconAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon-192.png");
            if (!File.Exists(iconPath))
            {
                throw new FileNotFoundException("Desktop icon asset was not found.", iconPath);
            }

            return Task.FromResult(iconPath);
        }

        private static async Task<string> CheckSyncChangeFeedAsync(
            DesktopSyncApplicationHost host,
            CancellationToken cancellationToken)
        {
            var response = await host.Sync.GetChangesAsync(sinceCursor: 0, limit: 1, cancellationToken)
                .ConfigureAwait(false);
            return "Ready; next cursor " + response.NextCursor.ToString(System.Globalization.CultureInfo.InvariantCulture);
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
                items.Add(new DesktopSelfTestItemSnapshot(
                    name,
                    false,
                    DesktopActionRequiredMessageResolver.FromException(exception)));
            }
        }

        private static async Task RollBackAddedSyncPairAsync(
            DesktopSyncApplicationHost host,
            Guid syncPairId,
            CancellationToken cancellationToken)
        {
            try
            {
                await host.App.StopSyncAsync(cancellationToken).ConfigureAwait(false);
                await host.App.DeleteSyncPairAsync(syncPairId, cancellationToken).ConfigureAwait(false);
                await host.App.StartSyncAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Trace.TraceWarning("Failed to roll back newly added Cotton Sync folder {0}: {1}", syncPairId, exception);
            }
        }

        private async Task SaveSyncPairSettingsAsync(
            SyncPairSettings syncPair,
            CancellationToken cancellationToken)
        {
            DesktopSyncApplicationHost? host = _host;
            if (host is null)
            {
                await _syncPairStore.UpsertAsync(syncPair, cancellationToken).ConfigureAwait(false);
                return;
            }

            SyncPairSaveResult result = await host.App
                .SaveSyncPairAsync(syncPair, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSaved)
            {
                throw new SyncPairValidationException(result.Validation.Errors);
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
                DateTime? persistedLastSyncedAtUtc = await stateStore
                    .GetPairLastSyncedAtUtcAsync(syncPairId, cancellationToken)
                    .ConfigureAwait(false);
                SyncChangeCursor cursor = await stateStore
                    .GetChangeCursorAsync(syncPairId, cancellationToken)
                    .ConfigureAwait(false);
                SyncPairStatus? status = currentStatus?.SyncPairs
                    .FirstOrDefault(pair => pair.SyncPairId == syncPair.Id);
                snapshots.Add(ToSnapshot(syncPair, persistedLastSyncedAtUtc, cursor, status));
            }

            return snapshots;
        }

        private static DesktopSyncPairSnapshot ToSnapshot(
            SyncPairSettings settings,
            DateTime? persistedLastSyncedAtUtc = null,
            SyncChangeCursor? cursor = null,
            SyncPairStatus? status = null)
        {
            DateTime? lastSyncedAtUtc = status?.LastSuccessfulSyncAtUtc;
            lastSyncedAtUtc ??= persistedLastSyncedAtUtc;
            return new DesktopSyncPairSnapshot(
                settings.Id,
                settings.DisplayName,
                settings.LocalRootPath,
                settings.RemoteDisplayPath,
                status is null ? settings.IsEnabled ? "Idle" : "Disabled" : ToStatusText(status),
                settings.RemoteRootNodeId,
                lastSyncedAtUtc,
                cursor?.LastCursor,
                status?.LastError,
                settings.Mode);
        }

        private static Uri ParseServerUrl(string serverUrl)
        {
            return DesktopServerUrl.NormalizeRequired(serverUrl, nameof(serverUrl));
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

        private DesktopSyncApplicationHost? DetachHost()
        {
            DesktopSyncApplicationHost? host = _host;
            _host = null;
            _activitySubscription?.Dispose();
            _activitySubscription = null;
            _sessionRevocationSubscription?.Dispose();
            _sessionRevocationSubscription = null;
            _statusSubscription?.Dispose();
            _statusSubscription = null;
            _transferProgressSubscription?.Dispose();
            _transferProgressSubscription = null;
            _runProgressSubscription?.Dispose();
            _runProgressSubscription = null;
            return host;
        }

        private async Task ReplaceHostAsync(DesktopSyncApplicationHost host, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DesktopSyncApplicationHost? previous = _host;
            _host = host;
            _activitySubscription?.Dispose();
            _sessionRevocationSubscription?.Dispose();
            _statusSubscription?.Dispose();
            _transferProgressSubscription?.Dispose();
            _runProgressSubscription?.Dispose();
            _statusSubscription = host.StatusPublisher.Subscribe(new StatusObserver(this));
            _activitySubscription = host.ActivityPublisher.Subscribe(new ActivityObserver(this));
            _sessionRevocationSubscription = host.SessionRevocationPublisher.Subscribe(new SessionRevocationObserver(this));
            _transferProgressSubscription = host.TransferProgressPublisher.Subscribe(new TransferProgressObserver(this));
            _runProgressSubscription = host.RunProgressPublisher.Subscribe(new RunProgressObserver(this));
            if (previous is not null)
            {
                await StopAndDisposeHostAsync(previous).ConfigureAwait(false);
            }
        }

        private static async Task StopAndDisposeHostAsync(DesktopSyncApplicationHost host)
        {
            try
            {
                await host.App.StopSyncAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Failed to stop previous desktop sync host: {0}", exception);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static DesktopSyncStatusSnapshot ToStatusSnapshot(SyncAppStatus status)
        {
            return new DesktopSyncStatusSnapshot(
                status.SyncPairs
                    .Select(static syncPair => new DesktopSyncPairStatusSnapshot(
                        syncPair.SyncPairId,
                        ToStatusText(syncPair),
                        syncPair.LastError,
                        syncPair.CurrentOperation,
                        syncPair.LastSuccessfulSyncAtUtc))
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

        private void OnActivityReported(AppSyncActivity activity)
        {
            ActivityReported?.Invoke(this, ToActivitySnapshot(activity));
        }

        private void OnSessionRevoked(SessionRevocationEvent sessionRevocation)
        {
            SessionRevoked?.Invoke(this, new DesktopSessionRevocationSnapshot(sessionRevocation.OccurredAtUtc));
        }

        private void OnTransferProgressChanged(AppTransferProgress progress)
        {
            TransferProgressChanged?.Invoke(this, ToTransferProgressSnapshot(progress));
        }

        private void OnRunProgressChanged(AppRunProgress progress)
        {
            RunProgressChanged?.Invoke(this, ToRunProgressSnapshot(progress));
        }

        private static DesktopActivitySnapshot ToActivitySnapshot(AppSyncActivity activity)
        {
            return new DesktopActivitySnapshot(
                activity.Type.ToString(),
                activity.ItemPath ?? string.Empty,
                activity.Message,
                activity.OccurredAtUtc,
                activity.SyncPairId);
        }

        private static DesktopTransferProgressSnapshot ToTransferProgressSnapshot(AppTransferProgress progress)
        {
            return new DesktopTransferProgressSnapshot(
                progress.SyncPairId,
                progress.Direction,
                progress.RelativePath,
                progress.TransferredBytes,
                progress.TotalBytes,
                progress.IsCompleted,
                progress.OccurredAtUtc,
                progress.SpeedBytesPerSecond,
                progress.EstimatedTimeRemaining);
        }

        private static DesktopRunProgressSnapshot ToRunProgressSnapshot(AppRunProgress progress)
        {
            return new DesktopRunProgressSnapshot(
                progress.SyncPairId,
                progress.Stage,
                progress.FilesCompleted,
                progress.FilesTotal,
                progress.CurrentPath,
                progress.StartedAtUtc,
                progress.IsCompleted,
                progress.OccurredAtUtc,
                progress.BytesCompleted,
                progress.BytesTotal);
        }

        private async Task<AuthSession?> TryRestoreSessionAsync(
            Uri serverUrl,
            CancellationToken cancellationToken)
        {
            if (!await CanUseStoredSessionAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            DesktopSyncApplicationHost host = _factory.Create(serverUrl);
            try
            {
                if (await host.TokenStore.GetAsync(cancellationToken).ConfigureAwait(false) is null)
                {
                    await host.DisposeAsync().ConfigureAwait(false);
                    return null;
                }

                using CancellationTokenSource restoreCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                restoreCancellation.CancelAfter(_savedSessionRestoreTimeout);
                AuthSession session = await host.App.RestoreSessionAsync(restoreCancellation.Token)
                    .WaitAsync(restoreCancellation.Token)
                    .ConfigureAwait(false);
                await ReplaceHostAsync(host, cancellationToken).ConfigureAwait(false);
                StartRestoredSessionSyncInBackground(host);
                return session;
            }
            catch (Cotton.Sdk.CottonApiException exception) when (IsAuthSessionRejected(exception))
            {
                Trace.TraceWarning("Failed to restore desktop session: {0}", exception);
                await host.TokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                await host.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            catch (Cotton.Sdk.CottonApiException)
            {
                await host.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceWarning(
                    "Timed out restoring desktop session for {0} after {1} seconds.",
                    serverUrl,
                    _savedSessionRestoreTimeout.TotalSeconds);
                await host.DisposeAsync().ConfigureAwait(false);
                throw new TimeoutException("Saved session could not be restored. Check connection to Cotton Cloud and retry.");
            }
            catch (HttpRequestException)
            {
                Trace.TraceWarning("Failed to restore desktop session because the server is unreachable: {0}", serverUrl);
                await host.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static bool IsAuthSessionRejected(Cotton.Sdk.CottonApiException exception)
        {
            return exception.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
        }

        private void StartRestoredSessionSyncInBackground(DesktopSyncApplicationHost host)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await host.App.StartSyncAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Trace.TraceWarning("Failed to start desktop sync after session restore: {0}", exception);
                    if (!ReferenceEquals(_host, host))
                    {
                        return;
                    }

                    ActivityReported?.Invoke(
                        this,
                        new DesktopActivitySnapshot(
                            "Error",
                            string.Empty,
                            DesktopActionRequiredMessageResolver.FromException(exception),
                            DateTime.UtcNow));
                }
            });
        }

        private async Task EnsureReleaseSecureTokenStorageAsync(CancellationToken cancellationToken)
        {
            DesktopTokenStorageCapabilitySnapshot tokenStorage = await _tokenStorageVerifier(cancellationToken)
                .ConfigureAwait(false);
            if (tokenStorage.IsReleaseSecure)
            {
                return;
            }

            throw new InvalidOperationException(CreateTokenStorageUnavailableMessage(tokenStorage));
        }

        private async Task<bool> CanUseStoredSessionAsync(CancellationToken cancellationToken)
        {
            DesktopTokenStorageCapabilitySnapshot tokenStorage;
            using CancellationTokenSource verificationCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            verificationCancellation.CancelAfter(_tokenStorageVerificationTimeout);
            try
            {
                tokenStorage = await _tokenStorageVerifier(verificationCancellation.Token)
                    .WaitAsync(verificationCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceWarning(
                    "Skipping desktop session restore because token storage verification timed out after {0} seconds.",
                    _tokenStorageVerificationTimeout.TotalSeconds);
                return false;
            }

            if (tokenStorage.IsReleaseSecure)
            {
                return true;
            }

            Trace.TraceWarning(
                "Skipping desktop session restore because token storage is not release secure: {0}",
                tokenStorage.Details);
            return false;
        }

        private static string CreateTokenStorageUnavailableMessage(DesktopTokenStorageCapabilitySnapshot tokenStorage)
        {
            return "Secure token storage is unavailable: "
                + tokenStorage.Details
                + ". Configure Windows DPAPI or Linux Secret Service before signing in.";
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

        private sealed class ActivityObserver : IObserver<AppSyncActivity>
        {
            private readonly DesktopShellController _controller;

            public ActivityObserver(DesktopShellController controller)
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

            public void OnNext(AppSyncActivity value)
            {
                _controller.OnActivityReported(value);
            }
        }

        private sealed class SessionRevocationObserver : IObserver<SessionRevocationEvent>
        {
            private readonly DesktopShellController _controller;

            public SessionRevocationObserver(DesktopShellController controller)
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

            public void OnNext(SessionRevocationEvent value)
            {
                _controller.OnSessionRevoked(value);
            }
        }

        private sealed class TransferProgressObserver : IObserver<AppTransferProgress>
        {
            private readonly DesktopShellController _controller;

            public TransferProgressObserver(DesktopShellController controller)
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

            public void OnNext(AppTransferProgress value)
            {
                _controller.OnTransferProgressChanged(value);
            }
        }

        private sealed class RunProgressObserver : IObserver<AppRunProgress>
        {
            private readonly DesktopShellController _controller;

            public RunProgressObserver(DesktopShellController controller)
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

            public void OnNext(AppRunProgress value)
            {
                _controller.OnRunProgressChanged(value);
            }
        }
    }
}
