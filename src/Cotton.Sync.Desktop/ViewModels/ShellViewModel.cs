// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Main desktop shell view model.
/// </summary>
internal sealed class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly IDesktopShellController _controller;
    private readonly ILocalFolderPicker _folderPicker;
    private readonly DesktopNotificationTracker _notificationTracker = new();
    private string _accountName = "Signed out";
    private string _actionRequiredMessage = string.Empty;
    private string _globalStatus = "Loading";
    private bool _isBusy;
    private bool _isSignedIn;
    private string _localFolderPath = string.Empty;
    private string _password = string.Empty;
    private string _remoteBrowserPath = "/";
    private string _remoteFolderPath = string.Empty;
    private bool _isApplyingStartWithOperatingSystem;
    private bool _isServerProbeChecking;
    private bool _isServerProbeFailed;
    private bool _isServerVerified;
    private bool _isAddSyncPairWizardVisible;
    private bool _isSettingsVisible;
    private bool _isLoadingSnapshot;
    private bool _isStartWithOperatingSystemSupported = true;
    private bool _isTrayLifecycleSupported;
    private string _trayLifecycleDetails = "Tray lifecycle is not supported on this platform yet.";
    private string _serverUrl = string.Empty;
    private string _serverProbeStatus = string.Empty;
    private bool _startWithOperatingSystem;
    private CancellationTokenSource? _serverProbeCancellation;
    private RemoteFolderRowViewModel? _selectedRemoteFolder;
    private SyncPairRowViewModel? _selectedSyncPair;
    private string _totpCode = string.Empty;
    private string _username = string.Empty;

    internal ShellViewModel(IDesktopShellController controller, ILocalFolderPicker folderPicker)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        SyncPairs.CollectionChanged += OnSyncPairsChanged;
        RemoteFolders.CollectionChanged += OnRemoteFoldersChanged;
        SelfTestItems.CollectionChanged += OnSelfTestItemsChanged;
        Notifications.CollectionChanged += OnNotificationsChanged;
        _controller.StatusChanged += OnStatusChanged;
        SignInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn, HandleCommandError);
        AddSyncPairCommand = new AsyncRelayCommand(AddSyncPairAsync, CanAddSyncPair, HandleCommandError);
        BrowseLocalFolderCommand = new AsyncRelayCommand(BrowseLocalFolderAsync, () => !IsBusy, HandleCommandError);
        CancelAddSyncPairCommand = new AsyncRelayCommand(CancelAddSyncPairAsync, () => !IsBusy, HandleCommandError);
        OpenRemoteFolderCommand = new AsyncRelayCommand(OpenRemoteFolderAsync, () => SelectedRemoteFolder is not null && !IsBusy, HandleCommandError);
        RemoteFolderUpCommand = new AsyncRelayCommand(RemoteFolderUpAsync, CanGoUpRemoteFolder, HandleCommandError);
        ShowAddSyncPairCommand = new AsyncRelayCommand(ShowAddSyncPairAsync, () => IsSignedIn && !IsBusy, HandleCommandError);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync, () => IsSignedIn && !IsBusy, HandleCommandError);
        CloseSettingsCommand = new AsyncRelayCommand(CloseSettingsAsync, () => !IsBusy, HandleCommandError);
        SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => IsSignedIn, HandleCommandError);
        PauseCommand = new AsyncRelayCommand(PauseAsync, () => IsSignedIn, HandleCommandError);
        ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => IsSignedIn, HandleCommandError);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => IsSignedIn, HandleCommandError);
        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, () => SelectedSyncPair is not null, HandleCommandError);
        OpenWebCommand = new AsyncRelayCommand(OpenWebAsync, () => IsSignedIn, HandleCommandError);
        SelfTestCommand = new AsyncRelayCommand(SelfTestAsync, () => !IsBusy, HandleCommandError);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => !IsBusy, HandleCommandError);
    }

    public ObservableCollection<SyncPairRowViewModel> SyncPairs { get; } = [];

    public ObservableCollection<ActivityRowViewModel> Activities { get; } = [];

    public ObservableCollection<RemoteFolderRowViewModel> RemoteFolders { get; } = [];

    public ObservableCollection<SelfTestItemRowViewModel> SelfTestItems { get; } = [];

    public ObservableCollection<DiagnosticItemRowViewModel> DiagnosticsItems { get; } = [];

    public ObservableCollection<NotificationRowViewModel> Notifications { get; } = [];

    public AsyncRelayCommand AddSyncPairCommand { get; }

    public AsyncRelayCommand BrowseLocalFolderCommand { get; }

    public AsyncRelayCommand CancelAddSyncPairCommand { get; }

    public AsyncRelayCommand CloseSettingsCommand { get; }

    public AsyncRelayCommand OpenFolderCommand { get; }

    public AsyncRelayCommand OpenWebCommand { get; }

    public AsyncRelayCommand OpenRemoteFolderCommand { get; }

    public AsyncRelayCommand PauseCommand { get; }

    public AsyncRelayCommand ResumeCommand { get; }

    public AsyncRelayCommand RemoteFolderUpCommand { get; }

    public AsyncRelayCommand SignInCommand { get; }

    public AsyncRelayCommand SignOutCommand { get; }

    public AsyncRelayCommand ShowAddSyncPairCommand { get; }

    public AsyncRelayCommand ShowSettingsCommand { get; }

    public AsyncRelayCommand SyncNowCommand { get; }

    public AsyncRelayCommand SelfTestCommand { get; }

    public AsyncRelayCommand ExportDiagnosticsCommand { get; }

    public string AccountName
    {
        get => _accountName;
        private set => SetProperty(ref _accountName, value);
    }

    public string AppVersion => typeof(ShellViewModel).Assembly.GetName().Version?.ToString() ?? "unknown";

    public string ActionRequiredMessage
    {
        get => _actionRequiredMessage;
        private set
        {
            if (SetProperty(ref _actionRequiredMessage, value))
            {
                OnPropertyChanged(nameof(HasActionRequired));
            }
        }
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        private set => SetProperty(ref _globalStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsSignedIn
    {
        get => _isSignedIn;
        private set
        {
            if (SetProperty(ref _isSignedIn, value))
            {
                OnPropertyChanged(nameof(IsDashboardVisible));
                OnPropertyChanged(nameof(IsSetupVisible));
                RaiseCommandStates();
            }
        }
    }

    public bool HasNoSyncPairs => SyncPairs.Count == 0;

    public bool HasActionRequired => !string.IsNullOrWhiteSpace(ActionRequiredMessage);

    public bool HasNoRemoteFolders => RemoteFolders.Count == 0;

    public bool HasRemoteFolders => RemoteFolders.Count > 0;

    public bool HasNoSelfTestItems => SelfTestItems.Count == 0;

    public bool HasSelfTestItems => SelfTestItems.Count > 0;

    public bool HasNotifications => Notifications.Count > 0;

    public bool HasSyncPairs => SyncPairs.Count > 0;

    public bool IsDashboardVisible => IsSignedIn;

    public bool IsSetupVisible => !IsSignedIn;

    public bool StartWithOperatingSystem
    {
        get => _startWithOperatingSystem;
        set
        {
            if (value && !IsStartWithOperatingSystemSupported)
            {
                return;
            }

            if (SetProperty(ref _startWithOperatingSystem, value) && !_isLoadingSnapshot)
            {
                _ = ApplyStartWithOperatingSystemAsync(value);
            }
        }
    }

    public bool IsStartWithOperatingSystemSupported
    {
        get => _isStartWithOperatingSystemSupported;
        private set
        {
            if (SetProperty(ref _isStartWithOperatingSystemSupported, value))
            {
                OnPropertyChanged(nameof(AutostartStatusText));
            }
        }
    }

    public bool IsTrayLifecycleSupported
    {
        get => _isTrayLifecycleSupported;
        private set
        {
            if (SetProperty(ref _isTrayLifecycleSupported, value))
            {
                OnPropertyChanged(nameof(IsTrayLifecycleUnsupported));
                OnPropertyChanged(nameof(AutostartStatusText));
                OnPropertyChanged(nameof(TrayLifecycleStatusText));
            }
        }
    }

    public bool IsTrayLifecycleUnsupported => !IsTrayLifecycleSupported;

    public string TrayLifecycleDetails
    {
        get => _trayLifecycleDetails;
        private set
        {
            if (SetProperty(ref _trayLifecycleDetails, value))
            {
                OnPropertyChanged(nameof(AutostartStatusText));
                OnPropertyChanged(nameof(TrayLifecycleStatusText));
            }
        }
    }

    public string AutostartStatusText
    {
        get
        {
            if (!IsStartWithOperatingSystemSupported)
            {
                return "Autostart is not available on this platform.";
            }

            return IsTrayLifecycleSupported
                ? "Cotton Sync can start minimized and keep running in the tray."
                : "Cotton Sync can start with your desktop session and opens as a normal window on this platform.";
        }
    }

    public string TrayLifecycleStatusText => IsTrayLifecycleSupported
        ? "Closing the window keeps Cotton Sync running from the tray."
        : TrayLifecycleDetails;

    public bool IsAddSyncPairWizardVisible
    {
        get => _isAddSyncPairWizardVisible;
        private set
        {
            if (SetProperty(ref _isAddSyncPairWizardVisible, value))
            {
                RaiseWizardStateProperties();
            }
        }
    }

    public bool HasLocalFolderSelection => !string.IsNullOrWhiteSpace(LocalFolderPath);

    public bool IsAddSyncPairLocalStepVisible => IsAddSyncPairWizardVisible && !HasLocalFolderSelection;

    public bool IsAddSyncPairCloudStepVisible => IsAddSyncPairWizardVisible && HasLocalFolderSelection;

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set => SetProperty(ref _isSettingsVisible, value);
    }

    public string AddSyncPairWizardTitle => HasLocalFolderSelection ? "Choose cloud folder" : "Choose local folder";

    public string AddSyncPairWizardSubtitle => HasLocalFolderSelection
        ? "Pick where this computer folder should sync in Cotton Cloud."
        : "Start with the folder on this computer.";

    public string RemoteFolderSelectionLabel => string.IsNullOrWhiteSpace(RemoteFolderPath)
        ? "Cloud folder: /"
        : $"Cloud folder: {RemoteFolderPath}";

    public bool IsServerProbeChecking
    {
        get => _isServerProbeChecking;
        private set => SetProperty(ref _isServerProbeChecking, value);
    }

    public bool IsServerProbeFailed
    {
        get => _isServerProbeFailed;
        private set => SetProperty(ref _isServerProbeFailed, value);
    }

    public bool IsServerVerified
    {
        get => _isServerVerified;
        private set
        {
            if (SetProperty(ref _isServerVerified, value))
            {
                SignInCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string LocalFolderPath
    {
        get => _localFolderPath;
        set
        {
            if (SetProperty(ref _localFolderPath, value))
            {
                AddSyncPairCommand.RaiseCanExecuteChanged();
                RaiseWizardStateProperties();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                SignInCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RemoteFolderPath
    {
        get => _remoteFolderPath;
        set
        {
            if (SetProperty(ref _remoteFolderPath, value))
            {
                AddSyncPairCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(RemoteFolderSelectionLabel));
            }
        }
    }

    public string RemoteBrowserPath
    {
        get => _remoteBrowserPath;
        private set
        {
            if (SetProperty(ref _remoteBrowserPath, value))
            {
                RemoteFolderUpCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (SetProperty(ref _serverUrl, value))
            {
                ScheduleServerProbe(value);
                SignInCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ServerProbeStatus
    {
        get => _serverProbeStatus;
        private set
        {
            if (SetProperty(ref _serverProbeStatus, value))
            {
                OnPropertyChanged(nameof(HasServerProbeStatus));
            }
        }
    }

    public bool HasServerProbeStatus => !string.IsNullOrWhiteSpace(ServerProbeStatus);

    public RemoteFolderRowViewModel? SelectedRemoteFolder
    {
        get => _selectedRemoteFolder;
        set
        {
            if (SetProperty(ref _selectedRemoteFolder, value))
            {
                OpenRemoteFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public SyncPairRowViewModel? SelectedSyncPair
    {
        get => _selectedSyncPair;
        set
        {
            if (SetProperty(ref _selectedSyncPair, value))
            {
                OpenFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TotpCode
    {
        get => _totpCode;
        set => SetProperty(ref _totpCode, value);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                SignInCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Dispose()
    {
        _controller.StatusChanged -= OnStatusChanged;
        SyncPairs.CollectionChanged -= OnSyncPairsChanged;
        RemoteFolders.CollectionChanged -= OnRemoteFoldersChanged;
        SelfTestItems.CollectionChanged -= OnSelfTestItemsChanged;
        Notifications.CollectionChanged -= OnNotificationsChanged;
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
        _controller.Dispose();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        _isLoadingSnapshot = true;
        try
        {
            DesktopShellSnapshot snapshot = await _controller.LoadAsync().ConfigureAwait(true);
            ServerUrl = snapshot.ServerUrl?.AbsoluteUri ?? string.Empty;
            Username = snapshot.RememberedUsername ?? string.Empty;
            IsStartWithOperatingSystemSupported = snapshot.PlatformCapabilities.IsAutostartSupported;
            IsTrayLifecycleSupported = snapshot.PlatformCapabilities.IsTrayLifecycleSupported;
            TrayLifecycleDetails = snapshot.PlatformCapabilities.TrayLifecycleDetails;
            StartWithOperatingSystem = snapshot.StartWithOperatingSystem;
            SyncPairs.Clear();
            foreach (DesktopSyncPairSnapshot syncPair in snapshot.SyncPairs)
            {
                SyncPairs.Add(ToRow(syncPair));
            }

            SelectedSyncPair = SyncPairs.FirstOrDefault();
            IsSignedIn = snapshot.IsSignedIn;
            AccountName = snapshot.AccountName ?? "Signed out";
            GlobalStatus = snapshot.IsSignedIn
                ? "Connected"
                : SyncPairs.Count == 0 ? "Ready to connect" : "Ready";
            AddActivity("App", string.Empty, "Settings loaded");
            if (snapshot.IsSignedIn)
            {
                AddActivity("Account", AccountName, "Session restored");
            }

            RefreshDiagnosticsItems();
            RaiseCommandStates();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleCommandError(exception);
        }
        finally
        {
            _isLoadingSnapshot = false;
            IsBusy = false;
        }
    }

    private async Task ApplyStartWithOperatingSystemAsync(bool enabled)
    {
        if (_isApplyingStartWithOperatingSystem)
        {
            return;
        }

        _isApplyingStartWithOperatingSystem = true;
        IsBusy = true;
        try
        {
            await _controller.SetStartWithOperatingSystemAsync(enabled).ConfigureAwait(true);
            AddActivity("App", string.Empty, enabled ? "Start with computer enabled" : "Start with computer disabled");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _isLoadingSnapshot = true;
            StartWithOperatingSystem = !enabled;
            _isLoadingSnapshot = false;
            HandleCommandError(exception);
        }
        finally
        {
            _isApplyingStartWithOperatingSystem = false;
            IsBusy = false;
        }
    }

    private async Task AddSyncPairAsync()
    {
        IsBusy = true;
        try
        {
            SyncPairSettings syncPair = await _controller.AddSyncPairAsync(
                new DesktopSyncPairRequest(LocalFolderPath, RemoteFolderPath)).ConfigureAwait(true);
            SyncPairRowViewModel row = ToRow(syncPair);
            SyncPairs.Add(row);
            SelectedSyncPair = row;
            LocalFolderPath = string.Empty;
            RemoteFolderPath = string.Empty;
            IsAddSyncPairWizardVisible = false;
            ActionRequiredMessage = string.Empty;
            RemoteFolders.Clear();
            GlobalStatus = "Sync requested";
            AddActivity("Pair", syncPair.LocalRootPath, "Folder added and initial sync requested");
            RefreshDiagnosticsItems();
            RaiseCommandStates();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BrowseLocalFolderAsync()
    {
        string? selectedPath = await _folderPicker.PickFolderAsync().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        LocalFolderPath = selectedPath;
        AddActivity("Folder", selectedPath, "Local folder selected");
    }

    private Task CancelAddSyncPairAsync()
    {
        LocalFolderPath = string.Empty;
        RemoteFolderPath = string.Empty;
        IsAddSyncPairWizardVisible = false;
        RemoteFolders.Clear();
        return Task.CompletedTask;
    }

    private bool CanGoUpRemoteFolder()
    {
        return !IsBusy && IsAddSyncPairWizardVisible && RemoteBrowserPath != "/";
    }

    private async Task OpenFolderAsync()
    {
        SyncPairRowViewModel? selected = SelectedSyncPair;
        if (selected is null)
        {
            return;
        }

        await _controller.OpenFolderAsync(selected.LocalPath).ConfigureAwait(true);
        AddActivity("Open", selected.LocalPath, "Folder opened");
    }

    private async Task OpenWebAsync()
    {
        await _controller.OpenWebAsync().ConfigureAwait(true);
        AddActivity("Open", string.Empty, "Cotton Cloud opened");
    }

    private async Task PauseAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.PauseAllAsync().ConfigureAwait(true);
            GlobalStatus = "Paused";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Paused");
            AddActivity("Sync", string.Empty, "Synchronization paused");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResumeAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.ResumeAllAsync().ConfigureAwait(true);
            GlobalStatus = "Ready";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Idle");
            AddActivity("Sync", string.Empty, "Synchronization resumed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInAsync()
    {
        IsBusy = true;
        try
        {
            AuthSession session = await _controller.SignInAsync(
                new DesktopSignInRequest(ServerUrl, Username, Password, TotpCode)).ConfigureAwait(true);
            IsSignedIn = true;
            AccountName = session.Email ?? session.Username;
            Password = string.Empty;
            GlobalStatus = "Connected";
            ActionRequiredMessage = string.Empty;
            AddActivity("Account", AccountName, "Signed in");
            RefreshDiagnosticsItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignOutAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.SignOutAsync().ConfigureAwait(true);
            IsSignedIn = false;
            AccountName = "Signed out";
            GlobalStatus = "Signed out";
            Password = string.Empty;
            IsAddSyncPairWizardVisible = false;
            IsSettingsVisible = false;
            ActionRequiredMessage = string.Empty;
            Notifications.Clear();
            _notificationTracker.Reset();
            RemoteFolders.Clear();
            SetAllPairStatuses("Idle");
            AddActivity("Account", string.Empty, "Signed out");
            RefreshDiagnosticsItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncNowAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.SyncAllAsync().ConfigureAwait(true);
            GlobalStatus = "Sync requested";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Sync requested");
            AddActivity("Sync", string.Empty, "Manual sync requested");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SelfTestAsync()
    {
        IsBusy = true;
        try
        {
            DesktopSelfTestSnapshot result = await _controller.RunSelfTestAsync().ConfigureAwait(true);
            GlobalStatus = result.Passed ? "Self-test passed" : "Action required";
            ActionRequiredMessage = DesktopActionRequiredMessageResolver.FromSelfTest(result);
            SelfTestItems.Clear();
            foreach (DesktopSelfTestItemSnapshot item in result.Items)
            {
                SelfTestItems.Add(new SelfTestItemRowViewModel
                {
                    Name = item.Name,
                    Details = item.Details,
                    Passed = item.Passed,
                });
                AddActivity(item.Passed ? "Check" : "Warning", item.Name, item.Passed ? item.Details : "Failed: " + item.Details);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportDiagnosticsAsync()
    {
        IsBusy = true;
        try
        {
            string bundlePath = await _controller.ExportDiagnosticsAsync().ConfigureAwait(true);
            GlobalStatus = "Diagnostics exported";
            ActionRequiredMessage = string.Empty;
            AddActivity("Diagnostics", bundlePath, "Diagnostics bundle exported");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ShowSettingsAsync()
    {
        IsSettingsVisible = true;
        return Task.CompletedTask;
    }

    private Task CloseSettingsAsync()
    {
        IsSettingsVisible = false;
        return Task.CompletedTask;
    }

    private async Task OpenRemoteFolderAsync()
    {
        RemoteFolderRowViewModel? selected = SelectedRemoteFolder;
        if (selected is null)
        {
            return;
        }

        await LoadRemoteFoldersAsync(selected.Path).ConfigureAwait(true);
    }

    private async Task RemoteFolderUpAsync()
    {
        await LoadRemoteFoldersAsync(GetRemoteParentPath(RemoteBrowserPath)).ConfigureAwait(true);
    }

    private async Task ShowAddSyncPairAsync()
    {
        IsAddSyncPairWizardVisible = true;
        await LoadRemoteFoldersAsync("/").ConfigureAwait(true);
    }

    private bool CanAddSyncPair()
    {
        return !IsBusy
            && IsSignedIn
            && !string.IsNullOrWhiteSpace(LocalFolderPath)
            && !string.IsNullOrWhiteSpace(RemoteFolderPath);
    }

    private bool CanSignIn()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(ServerUrl)
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrEmpty(Password)
            && IsServerVerified;
    }

    private void HandleCommandError(Exception exception)
    {
        Trace.TraceError(exception.ToString());
        GlobalStatus = "Action failed";
        ActionRequiredMessage = exception.Message;
        AddActivity("Error", string.Empty, exception.Message);
        IsBusy = false;
    }

    private async Task ProbeServerAfterDelayAsync(string serverUrl, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            DesktopServerProbeResult result = await _controller.ProbeServerAsync(serverUrl, cancellationToken)
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyServerProbeResult(result),
                DispatcherPriority.Normal,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Failed to probe Cotton server {0}: {1}", serverUrl, exception);
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyServerProbeFailure(),
                DispatcherPriority.Normal);
        }
    }

    private void ApplyServerProbeFailure()
    {
        IsServerProbeChecking = false;
        IsServerVerified = false;
        IsServerProbeFailed = true;
        ServerProbeStatus = "Cotton server not found";
    }

    private void ApplyServerProbeResult(DesktopServerProbeResult result)
    {
        IsServerProbeChecking = false;
        IsServerVerified = result.IsCottonServer;
        IsServerProbeFailed = !result.IsCottonServer;
        ServerProbeStatus = result.IsCottonServer
            ? "Cotton Cloud"
            : "Cotton server not found";
    }

    private void ResetServerProbe()
    {
        IsServerProbeChecking = false;
        IsServerVerified = false;
        IsServerProbeFailed = false;
        ServerProbeStatus = string.Empty;
    }

    private void ScheduleServerProbe(string serverUrl)
    {
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
        string normalized = serverUrl.Trim();
        if (normalized.Length == 0)
        {
            _serverProbeCancellation = null;
            ResetServerProbe();
            return;
        }

        _serverProbeCancellation = new CancellationTokenSource();
        IsServerProbeChecking = true;
        IsServerVerified = false;
        IsServerProbeFailed = false;
        ServerProbeStatus = "Checking server";
        _ = ProbeServerAfterDelayAsync(normalized, _serverProbeCancellation.Token);
    }

    private void OnSyncPairsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoSyncPairs));
        OnPropertyChanged(nameof(HasSyncPairs));
        OpenFolderCommand.RaiseCanExecuteChanged();
        RefreshDiagnosticsItems();
    }

    private void OnRemoteFoldersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoRemoteFolders));
        OnPropertyChanged(nameof(HasRemoteFolders));
        OpenRemoteFolderCommand.RaiseCanExecuteChanged();
    }

    private void OnSelfTestItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoSelfTestItems));
        OnPropertyChanged(nameof(HasSelfTestItems));
    }

    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNotifications));
    }

    private async Task LoadRemoteFoldersAsync(string remotePath)
    {
        IsBusy = true;
        try
        {
            DesktopRemoteFolderListSnapshot folders = await _controller
                .ListRemoteFoldersAsync(remotePath)
                .ConfigureAwait(true);
            RemoteBrowserPath = folders.CurrentPath;
            RemoteFolderPath = folders.CurrentPath;
            RemoteFolders.Clear();
            foreach (DesktopRemoteFolderSnapshot folder in folders.Folders)
            {
                RemoteFolders.Add(new RemoteFolderRowViewModel
                {
                    Id = folder.Id,
                    Name = folder.Name,
                    Path = folder.Path,
                });
            }

            SelectedRemoteFolder = RemoteFolders.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnStatusChanged(object? sender, DesktopSyncStatusSnapshot status)
    {
        Dispatcher.UIThread.Post(() => ApplyStatus(status));
    }

    private void ApplyStatus(DesktopSyncStatusSnapshot status)
    {
        foreach (DesktopSyncPairStatusSnapshot pairStatus in status.SyncPairs)
        {
            SyncPairRowViewModel? row = SyncPairs.FirstOrDefault(syncPair => syncPair.Id == pairStatus.Id);
            if (row is null)
            {
                continue;
            }

            row.Status = pairStatus.Status;
            row.LastError = pairStatus.LastError;
            if (!string.IsNullOrWhiteSpace(pairStatus.LastError))
            {
                AddActivity("Error", row.LocalPath, pairStatus.LastError);
            }
        }

        GlobalStatus = ResolveGlobalStatus(status);
        ActionRequiredMessage = DesktopActionRequiredMessageResolver.FromStatus(status);
        AddNotifications(_notificationTracker.Apply(status, SyncPairs.ToDictionary(static pair => pair.Id, static pair => pair.DisplayName)));
        RefreshDiagnosticsItems();
    }

    private void AddNotifications(IReadOnlyList<DesktopNotificationRequest> requests)
    {
        foreach (DesktopNotificationRequest request in requests)
        {
            Notifications.Insert(0, new NotificationRowViewModel
            {
                Title = request.Title,
                Message = request.Message,
            });
            AddActivity("Notification", string.Empty, request.Message);
        }

        while (Notifications.Count > 3)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }
    }

    private string ResolveGlobalStatus(DesktopSyncStatusSnapshot status)
    {
        if (!IsSignedIn)
        {
            return "Signed out";
        }

        if (status.SyncPairs.Any(static pair => string.Equals(pair.Status, "Error", StringComparison.Ordinal)))
        {
            return "Action required";
        }

        if (status.SyncPairs.Any(static pair => string.Equals(pair.Status, "Syncing", StringComparison.Ordinal)
            || string.Equals(pair.Status, "Scanning", StringComparison.Ordinal)))
        {
            return "Syncing";
        }

        if (status.SyncPairs.Count > 0
            && status.SyncPairs.All(static pair => string.Equals(pair.Status, "Paused", StringComparison.Ordinal)))
        {
            return "Paused";
        }

        return "Connected";
    }

    private void AddActivity(string kind, string path, string details)
    {
        Activities.Insert(0, new ActivityRowViewModel
        {
            Time = DateTimeOffset.Now.ToString("HH:mm", CultureInfo.CurrentCulture),
            Kind = kind,
            Path = path,
            Details = details,
        });
    }

    private void RaiseCommandStates()
    {
        SignInCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
        AddSyncPairCommand.RaiseCanExecuteChanged();
        BrowseLocalFolderCommand.RaiseCanExecuteChanged();
        CancelAddSyncPairCommand.RaiseCanExecuteChanged();
        OpenRemoteFolderCommand.RaiseCanExecuteChanged();
        RemoteFolderUpCommand.RaiseCanExecuteChanged();
        SyncNowCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        OpenWebCommand.RaiseCanExecuteChanged();
        ShowAddSyncPairCommand.RaiseCanExecuteChanged();
        ShowSettingsCommand.RaiseCanExecuteChanged();
        CloseSettingsCommand.RaiseCanExecuteChanged();
        SelfTestCommand.RaiseCanExecuteChanged();
        ExportDiagnosticsCommand.RaiseCanExecuteChanged();
    }

    private void RaiseWizardStateProperties()
    {
        OnPropertyChanged(nameof(HasLocalFolderSelection));
        OnPropertyChanged(nameof(IsAddSyncPairLocalStepVisible));
        OnPropertyChanged(nameof(IsAddSyncPairCloudStepVisible));
        OnPropertyChanged(nameof(AddSyncPairWizardTitle));
        OnPropertyChanged(nameof(AddSyncPairWizardSubtitle));
    }

    private void SetAllPairStatuses(string status)
    {
        foreach (SyncPairRowViewModel syncPair in SyncPairs)
        {
            syncPair.Status = status;
        }
    }

    private static SyncPairRowViewModel ToRow(SyncPairSettings syncPair)
    {
        return new SyncPairRowViewModel
        {
            Id = syncPair.Id,
            DisplayName = syncPair.DisplayName,
            LocalPath = syncPair.LocalRootPath,
            RemoteRootNodeId = syncPair.RemoteRootNodeId,
            RemotePath = syncPair.RemoteDisplayPath,
            Status = syncPair.IsEnabled ? "Idle" : "Disabled",
        };
    }

    private static SyncPairRowViewModel ToRow(DesktopSyncPairSnapshot syncPair)
    {
        return new SyncPairRowViewModel
        {
            Id = syncPair.Id,
            DisplayName = syncPair.DisplayName,
            LocalPath = syncPair.LocalPath,
            RemoteRootNodeId = syncPair.RemoteRootNodeId,
            RemotePath = syncPair.RemotePath,
            Status = syncPair.Status,
            LastSyncedAtUtc = syncPair.LastSyncedAtUtc,
            ChangeCursor = syncPair.ChangeCursor,
            LastError = syncPair.LastError,
        };
    }

    private void RefreshDiagnosticsItems()
    {
        DiagnosticsItems.Clear();
        AddDiagnosticItem("App version", AppVersion);
        AddDiagnosticItem("Server", string.IsNullOrWhiteSpace(ServerUrl) ? "Not configured" : ServerUrl);
        AddDiagnosticItem("Account", AccountName);
        AddDiagnosticItem("Sync pairs", SyncPairs.Count.ToString(CultureInfo.InvariantCulture));
        foreach (SyncPairRowViewModel syncPair in SyncPairs)
        {
            AddDiagnosticItem(syncPair.DisplayName + " id", syncPair.Id.ToString());
            AddDiagnosticItem(syncPair.DisplayName + " local", syncPair.LocalPath);
            AddDiagnosticItem(syncPair.DisplayName + " remote", syncPair.RemotePath);
            AddDiagnosticItem(
                syncPair.DisplayName + " remote id",
                syncPair.RemoteRootNodeId?.ToString() ?? "Unknown");
            AddDiagnosticItem(syncPair.DisplayName + " status", syncPair.Status);
            AddDiagnosticItem(syncPair.DisplayName + " last sync", FormatDiagnosticUtc(syncPair.LastSyncedAtUtc));
            AddDiagnosticItem(
                syncPair.DisplayName + " cursor",
                syncPair.ChangeCursor?.ToString(CultureInfo.InvariantCulture) ?? "0");
            AddDiagnosticItem(
                syncPair.DisplayName + " last error",
                string.IsNullOrWhiteSpace(syncPair.LastError) ? "None" : syncPair.LastError);
        }
    }

    private void AddDiagnosticItem(string label, string value)
    {
        DiagnosticsItems.Add(new DiagnosticItemRowViewModel
        {
            Label = label,
            Value = value,
        });
    }

    private static string FormatDiagnosticUtc(DateTime? value)
    {
        return value is null
            ? "Never"
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("u", CultureInfo.InvariantCulture);
    }

    private static string GetRemoteParentPath(string remotePath)
    {
        string normalized = string.IsNullOrWhiteSpace(remotePath)
            ? "/"
            : "/" + remotePath.Replace('\\', '/').Trim('/');
        if (normalized == "/")
        {
            return "/";
        }

        int lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }
}
