// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using Cotton.Sync.App.Auth;
using Cotton.Sync.App.Preferences;
using Cotton.Sync.App.SyncPairs;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Main desktop shell view model.
/// </summary>
internal sealed class ShellViewModel : ViewModelBase, IDisposable, IAsyncDisposable
{
    private const int MaxActivityRows = 30;
    private const int MaxConflictRows = 20;

    private readonly IDesktopShellController _controller;
    private readonly DesktopFeatureFlags _featureFlags;
    private readonly ILocalFolderPicker _folderPicker;
    private readonly IDesktopNotificationService _notificationService;
    private readonly IDesktopThemeService _themeService;
    private readonly IDesktopUiDispatcher _uiDispatcher;
    private readonly DesktopNotificationTracker _notificationTracker = new();
    private readonly SyncPairSettingsValidator _syncPairSettingsValidator = new();
    private readonly Dictionary<Guid, string> _lastStatusErrorActivityMessages = [];
    private string _accountName = "Signed out";
    private string _actionRequiredMessage = string.Empty;
    private string _currentProgressText = "Sign in to start sync.";
    private string _currentRunProgressDetails = string.Empty;
    private string _currentRunProgressTitle = string.Empty;
    private string _currentTransferDetails = string.Empty;
    private string _currentTransferTitle = string.Empty;
    private string _dataDirectory = string.Empty;
    private string _appDatabasePath = string.Empty;
    private string _syncStateDatabasePath = string.Empty;
    private string _tokenStorePath = string.Empty;
    private string _globalStatus = "Loading";
    private bool _hasCurrentRunProgress;
    private bool _hasCurrentTransfer;
    private bool _isBusy;
    private bool _isCurrentRunProgressIndeterminate;
    private bool _isCurrentTransferIndeterminate;
    private bool _isSignedIn;
    private string _lastDiagnosticsBundlePath = string.Empty;
    private string _localFolderPath = string.Empty;
    private string _newRemoteFolderName = string.Empty;
    private string _password = string.Empty;
    private string _remoteBrowserPath = "/";
    private string _remoteFolderPath = string.Empty;
    private bool _enableNotifications = true;
    private bool _isApplyingNotificationPreference;
    private bool _isApplyingStartWithOperatingSystem;
    private bool _isApplyingThemePreference;
    private bool _isServerProbeChecking;
    private bool _isServerProbeFailed;
    private bool _isServerVerified;
    private bool _isAddSyncPairWizardVisible;
    private bool _isCreateRemoteFolderVisible;
    private bool _isDesktopSyncChangesApiUnavailable;
    private bool _isLocalFolderSelectionError;
    private bool _isSelectedSyncPairEditorVisible;
    private bool _isSettingsVisible;
    private bool _isLoadingSnapshot;
    private bool _isStartWithOperatingSystemSupported = true;
    private bool _isTrayLifecycleSupported;
    private int _selectedSettingsTabIndex;
    private string _trayLifecycleDetails = "Tray lifecycle is not supported on this platform yet.";
    private string _serverUrl = string.Empty;
    private string _serverProbeStatus = string.Empty;
    private bool _startWithOperatingSystem;
    private AppThemeMode _themeMode = AppThemeMode.Dark;
    private double _currentRunProgressValue;
    private double _currentTransferProgressValue;
    private CancellationTokenSource? _serverProbeCancellation;
    private ConflictRowViewModel? _selectedConflict;
    private RemoteFolderRowViewModel? _selectedRemoteFolder;
    private SyncPairRowViewModel? _selectedSyncPair;
    private SyncPairRowViewModel? _pendingRemoveSyncPair;
    private string _totpCode = string.Empty;
    private SyncTransferDirection _transferDirection = SyncTransferDirection.Unknown;
    private Guid? _transferSyncPairId;
    private string _transferRelativePath = string.Empty;
    private string _username = string.Empty;

    internal ShellViewModel(
        IDesktopShellController controller,
        ILocalFolderPicker folderPicker,
        IDesktopNotificationService notificationService,
        IDesktopThemeService themeService,
        IDesktopUiDispatcher? uiDispatcher = null,
        DesktopFeatureFlags? featureFlags = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _featureFlags = featureFlags ?? DesktopFeatureFlags.Default;
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _uiDispatcher = uiDispatcher ?? new AvaloniaDesktopUiDispatcher();
        Activities.CollectionChanged += OnActivitiesChanged;
        Conflicts.CollectionChanged += OnConflictsChanged;
        SyncPairs.CollectionChanged += OnSyncPairsChanged;
        RemoteFolders.CollectionChanged += OnRemoteFoldersChanged;
        SelfTestItems.CollectionChanged += OnSelfTestItemsChanged;
        Notifications.CollectionChanged += OnNotificationsChanged;
        _controller.ActivityReported += OnActivityReported;
        _controller.TransferProgressChanged += OnTransferProgressChanged;
        _controller.RunProgressChanged += OnRunProgressChanged;
        _controller.StatusChanged += OnStatusChanged;
        SignInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn, HandleCommandError);
        ChangeServerCommand = new AsyncRelayCommand(ChangeServerAsync, () => !IsBusy, HandleCommandError);
        AddSyncPairCommand = new AsyncRelayCommand(AddSyncPairAsync, CanAddSyncPair, HandleCommandError);
        BrowseLocalFolderCommand = new AsyncRelayCommand(BrowseLocalFolderAsync, CanBrowseLocalFolder, HandleCommandError);
        CancelAddSyncPairCommand = new AsyncRelayCommand(CancelAddSyncPairAsync, () => !IsBusy, HandleCommandError);
        CancelCreateRemoteFolderCommand = new AsyncRelayCommand(CancelCreateRemoteFolderAsync, () => !IsBusy, HandleCommandError);
        CreateRemoteFolderCommand = new AsyncRelayCommand(CreateRemoteFolderAsync, CanCreateRemoteFolder, HandleCommandError);
        OpenRemoteFolderCommand = new AsyncRelayCommand(OpenRemoteFolderAsync, CanOpenRemoteFolder, HandleCommandError);
        RemoteFolderUpCommand = new AsyncRelayCommand(RemoteFolderUpAsync, CanGoUpRemoteFolder, HandleCommandError);
        ShowCreateRemoteFolderCommand = new AsyncRelayCommand(ShowCreateRemoteFolderAsync, CanShowCreateRemoteFolder, HandleCommandError);
        ShowAddSyncPairCommand = new AsyncRelayCommand(ShowAddSyncPairAsync, CanShowAddSyncPair, HandleCommandError);
        ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync, () => IsSignedIn && !IsBusy, HandleCommandError);
        CloseSettingsCommand = new AsyncRelayCommand(CloseSettingsAsync, () => !IsBusy, HandleCommandError);
        SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => CanSyncNow, HandleCommandError);
        PauseCommand = new AsyncRelayCommand(PauseAsync, () => CanPauseSync, HandleCommandError);
        ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => CanResumeSync, HandleCommandError);
        PauseResumeCommand = new AsyncRelayCommand(PauseResumeAsync, () => CanTogglePauseResumeSync, HandleCommandError);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => IsSignedIn, HandleCommandError);
        OpenFolderCommand = new AsyncRelayCommand(
            OpenFolderAsync,
            parameter => ResolveOpenFolderTarget(parameter) is not null,
            HandleCommandError);
        OpenTrayFolderCommand = new AsyncRelayCommand(
            OpenTrayFolderAsync,
            () => CanOpenTrayFolder,
            HandleCommandError);
        OpenConflictCommand = new AsyncRelayCommand(
            OpenConflictAsync,
            parameter => parameter is ConflictRowViewModel && !IsBusy,
            HandleCommandError);
        ToggleSelectedSyncPairEnabledCommand = new AsyncRelayCommand(
            ToggleSelectedSyncPairEnabledAsync,
            () => IsSignedIn && SelectedSyncPair is not null && !IsBusy,
            HandleCommandError);
        SaveSelectedSyncPairNameCommand = new AsyncRelayCommand(
            SaveSelectedSyncPairNameAsync,
            () => IsSignedIn && SelectedSyncPair is not null && !IsBusy,
            HandleCommandError);
        RemoveSelectedSyncPairCommand = new AsyncRelayCommand(
            RequestRemoveSelectedSyncPairAsync,
            () => IsSignedIn && SelectedSyncPair is not null && !IsBusy && !IsRemoveSyncPairConfirmationVisible,
            HandleCommandError);
        ShowSelectedSyncPairEditorCommand = new AsyncRelayCommand(
            ShowSelectedSyncPairEditorAsync,
            parameter => IsSignedIn && ResolveSyncPairTarget(parameter) is not null && !IsBusy,
            HandleCommandError);
        CancelSelectedSyncPairEditorCommand = new AsyncRelayCommand(
            CancelSelectedSyncPairEditorAsync,
            () => IsSelectedSyncPairEditorVisible && !IsBusy,
            HandleCommandError);
        ConfirmRemoveSelectedSyncPairCommand = new AsyncRelayCommand(
            ConfirmRemoveSelectedSyncPairAsync,
            () => IsSignedIn && _pendingRemoveSyncPair is not null && !IsBusy,
            HandleCommandError);
        CancelRemoveSyncPairCommand = new AsyncRelayCommand(
            CancelRemoveSyncPairAsync,
            () => _pendingRemoveSyncPair is not null && !IsBusy,
            HandleCommandError);
        OpenWebCommand = new AsyncRelayCommand(OpenWebAsync, () => IsSignedIn, HandleCommandError);
        SelfTestCommand = new AsyncRelayCommand(SelfTestAsync, () => !IsBusy, HandleCommandError);
        ExportDiagnosticsCommand = new AsyncRelayCommand(ExportDiagnosticsAsync, () => !IsBusy, HandleCommandError);
        OpenDataFolderCommand = new AsyncRelayCommand(
            OpenDataFolderAsync,
            () => HasDataDirectory && !IsBusy,
            HandleCommandError);
        OpenDiagnosticsBundleFolderCommand = new AsyncRelayCommand(
            OpenDiagnosticsBundleFolderAsync,
            () => HasLastDiagnosticsBundlePath && !IsBusy,
            HandleCommandError);
    }

    public ObservableCollection<SyncPairRowViewModel> SyncPairs { get; } = [];

    public ObservableCollection<ActivityRowViewModel> Activities { get; } = [];

    public ObservableCollection<ConflictRowViewModel> Conflicts { get; } = [];

    public ObservableCollection<RemoteFolderRowViewModel> RemoteFolders { get; } = [];

    public ObservableCollection<SelfTestItemRowViewModel> SelfTestItems { get; } = [];

    public ObservableCollection<DiagnosticItemRowViewModel> DiagnosticsItems { get; } = [];

    public ObservableCollection<NotificationRowViewModel> Notifications { get; } = [];

    public AsyncRelayCommand AddSyncPairCommand { get; }

    public AsyncRelayCommand BrowseLocalFolderCommand { get; }

    public AsyncRelayCommand CancelAddSyncPairCommand { get; }

    public AsyncRelayCommand CancelCreateRemoteFolderCommand { get; }

    public AsyncRelayCommand CancelRemoveSyncPairCommand { get; }

    public AsyncRelayCommand CancelSelectedSyncPairEditorCommand { get; }

    public AsyncRelayCommand ChangeServerCommand { get; }

    public AsyncRelayCommand CloseSettingsCommand { get; }

    public AsyncRelayCommand ConfirmRemoveSelectedSyncPairCommand { get; }

    public AsyncRelayCommand CreateRemoteFolderCommand { get; }

    public AsyncRelayCommand OpenDiagnosticsBundleFolderCommand { get; }

    public AsyncRelayCommand OpenDataFolderCommand { get; }

    public AsyncRelayCommand OpenFolderCommand { get; }

    public AsyncRelayCommand OpenConflictCommand { get; }

    public AsyncRelayCommand OpenTrayFolderCommand { get; }

    public AsyncRelayCommand OpenWebCommand { get; }

    public AsyncRelayCommand OpenRemoteFolderCommand { get; }

    public AsyncRelayCommand RemoveSelectedSyncPairCommand { get; }

    public AsyncRelayCommand SaveSelectedSyncPairNameCommand { get; }

    public AsyncRelayCommand ToggleSelectedSyncPairEnabledCommand { get; }

    public AsyncRelayCommand PauseCommand { get; }

    public AsyncRelayCommand PauseResumeCommand { get; }

    public AsyncRelayCommand ResumeCommand { get; }

    public AsyncRelayCommand RemoteFolderUpCommand { get; }

    public AsyncRelayCommand SignInCommand { get; }

    public AsyncRelayCommand SignOutCommand { get; }

    public AsyncRelayCommand ShowAddSyncPairCommand { get; }

    public AsyncRelayCommand ShowCreateRemoteFolderCommand { get; }

    public AsyncRelayCommand ShowSelectedSyncPairEditorCommand { get; }

    public AsyncRelayCommand ShowSettingsCommand { get; }

    public AsyncRelayCommand SyncNowCommand { get; }

    public AsyncRelayCommand SelfTestCommand { get; }

    public AsyncRelayCommand ExportDiagnosticsCommand { get; }

    public string AccountName
    {
        get => _accountName;
        private set
        {
            if (SetProperty(ref _accountName, value))
            {
                OnPropertyChanged(nameof(HeaderTitleText));
            }
        }
    }

    public string AppVersion => DesktopAppVersion.Current;

    public string ActionRequiredMessage
    {
        get => _actionRequiredMessage;
        private set
        {
            if (SetProperty(ref _actionRequiredMessage, value))
            {
                if (IsMissingDesktopSyncChangesApiMessage(value))
                {
                    SetDesktopSyncChangesApiUnavailable(true);
                }

                OnPropertyChanged(nameof(HasActionRequired));
                OnPropertyChanged(nameof(HasStatusAttention));
                OnPropertyChanged(nameof(IsStatusCardVisible));
                OnPropertyChanged(nameof(ActionRequiredOpacity));
                OnPropertyChanged(nameof(CanRetryActionRequired));
                OnPropertyChanged(nameof(StatusCardTitle));
                OnPropertyChanged(nameof(StatusCardDetailText));
                OnPropertyChanged(nameof(HasStatusCardDetail));
                RaiseAddSyncPairFlowCommandStates();
                RefreshCurrentProgressText();
            }
        }
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        private set
        {
            if (SetProperty(ref _globalStatus, value))
            {
                OnPropertyChanged(nameof(HeaderStatusText));
                OnPropertyChanged(nameof(StatusCardTitle));
            }
        }
    }

    public string HeaderStatusText => HasConflicts ? "Conflicts need review" : GlobalStatus;

    public string HeaderTitleText => IsSignedIn ? ResolveAccountDisplayName(AccountName, null) : "Cotton Sync";

    public string StatusCardTitle
    {
        get
        {
            if (HasActionRequired)
            {
                return "Sync needs attention";
            }

            return CurrentProgressText;
        }
    }

    public string StatusCardDetailText => HasActionRequired ? CurrentProgressText : string.Empty;

    public bool HasStatusCardDetail => !string.IsNullOrWhiteSpace(StatusCardDetailText);

    public string CurrentProgressText
    {
        get => _currentProgressText;
        private set
        {
            if (SetProperty(ref _currentProgressText, value))
            {
                OnPropertyChanged(nameof(StatusCardTitle));
                OnPropertyChanged(nameof(StatusCardDetailText));
                OnPropertyChanged(nameof(HasStatusCardDetail));
            }
        }
    }

    public bool HasCurrentTransfer
    {
        get => _hasCurrentTransfer;
        private set
        {
            if (SetProperty(ref _hasCurrentTransfer, value))
            {
                OnPropertyChanged(nameof(IsCurrentTransferDeterminate));
            }
        }
    }

    public string CurrentTransferTitle
    {
        get => _currentTransferTitle;
        private set => SetProperty(ref _currentTransferTitle, value);
    }

    public string CurrentTransferDetails
    {
        get => _currentTransferDetails;
        private set => SetProperty(ref _currentTransferDetails, value);
    }

    public double CurrentTransferProgressValue
    {
        get => _currentTransferProgressValue;
        private set => SetProperty(ref _currentTransferProgressValue, value);
    }

    public bool IsCurrentTransferIndeterminate
    {
        get => _isCurrentTransferIndeterminate;
        private set
        {
            if (SetProperty(ref _isCurrentTransferIndeterminate, value))
            {
                OnPropertyChanged(nameof(IsCurrentTransferDeterminate));
            }
        }
    }

    public bool IsCurrentTransferDeterminate => HasCurrentTransfer && !IsCurrentTransferIndeterminate;

    public bool HasCurrentRunProgress
    {
        get => _hasCurrentRunProgress;
        private set
        {
            if (SetProperty(ref _hasCurrentRunProgress, value))
            {
                OnPropertyChanged(nameof(IsCurrentRunProgressDeterminate));
            }
        }
    }

    public string CurrentRunProgressTitle
    {
        get => _currentRunProgressTitle;
        private set => SetProperty(ref _currentRunProgressTitle, value);
    }

    public string CurrentRunProgressDetails
    {
        get => _currentRunProgressDetails;
        private set => SetProperty(ref _currentRunProgressDetails, value);
    }

    public double CurrentRunProgressValue
    {
        get => _currentRunProgressValue;
        private set => SetProperty(ref _currentRunProgressValue, value);
    }

    public bool IsCurrentRunProgressIndeterminate
    {
        get => _isCurrentRunProgressIndeterminate;
        private set
        {
            if (SetProperty(ref _isCurrentRunProgressIndeterminate, value))
            {
                OnPropertyChanged(nameof(IsCurrentRunProgressDeterminate));
            }
        }
    }

    public bool IsCurrentRunProgressDeterminate => HasCurrentRunProgress && !IsCurrentRunProgressIndeterminate;

    public bool HasCurrentWorkProgress => HasCurrentTransfer || HasCurrentRunProgress;

    public string CurrentWorkProgressTitle => HasCurrentTransfer ? CurrentTransferTitle : CurrentRunProgressTitle;

    public string CurrentWorkProgressDetails => HasCurrentTransfer ? CurrentTransferDetails : CurrentRunProgressDetails;

    public string CurrentWorkProgressSecondaryDetails => HasCurrentTransfer && HasCurrentRunProgress
        ? CurrentRunProgressDetails
        : string.Empty;

    public bool HasCurrentWorkProgressSecondaryDetails => !string.IsNullOrWhiteSpace(CurrentWorkProgressSecondaryDetails);

    public double CurrentWorkProgressValue => HasCurrentTransfer ? CurrentTransferProgressValue : CurrentRunProgressValue;

    public bool IsCurrentWorkProgressIndeterminate => HasCurrentTransfer
        ? IsCurrentTransferIndeterminate
        : IsCurrentRunProgressIndeterminate;

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
                OnPropertyChanged(nameof(HeaderTitleText));
                RaiseSetupStateProperties();
                OnPropertyChanged(nameof(CanRetryActionRequired));
                RefreshCurrentProgressText();
                RaiseCommandStates();
            }
        }
    }

    public bool HasNoSyncPairs => SyncPairs.Count == 0;

    public bool HasNoActivities => Activities.Count == 0;

    public bool HasActivities => Activities.Count > 0;

    public bool HasConflicts => Conflicts.Count > 0;

    public string ConflictCountLabel => Conflicts.Count == 1 ? "1 conflict" : Conflicts.Count + " conflicts";

    public bool HasActionRequired => !string.IsNullOrWhiteSpace(ActionRequiredMessage);

    public bool HasStatusAttention => HasActionRequired || HasConflicts;

    public bool IsStatusCardVisible => HasSyncPairs && !HasActionRequired && !HasConflicts && !HasCurrentWorkProgress;

    public bool IsDashboardChromeVisible => !IsAddSyncPairWizardVisible && !IsSettingsVisible;

    public double ActionRequiredOpacity => HasActionRequired ? 1 : 0;

    public bool CanRetryActionRequired => HasActionRequired && IsSignedIn;

    public bool HasNoRemoteFolders => RemoteFolders.Count == 0;

    public bool HasRemoteFolders => RemoteFolders.Count > 0;

    public bool HasNoSelfTestItems => SelfTestItems.Count == 0;

    public bool HasSelfTestItems => SelfTestItems.Count > 0;

    public bool HasNotifications => Notifications.Count > 0;

    public bool HasSyncPairs => SyncPairs.Count > 0;

    public bool IsSelectedSyncPairEditorVisible
    {
        get => _isSelectedSyncPairEditorVisible;
        private set
        {
            if (SetProperty(ref _isSelectedSyncPairEditorVisible, value))
            {
                UpdateSelectedSyncPairEditorVisibility();
                CancelSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
                RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanSyncNow => IsSignedIn && !IsBusy && HasEnabledSyncPairs && !IsSyncPaused;

    public bool CanPauseSync => IsSignedIn && !IsBusy && HasEnabledSyncPairs && !IsSyncPaused;

    public bool CanResumeSync => IsSignedIn && !IsBusy && IsSyncPaused;

    public bool CanTogglePauseResumeSync => IsSignedIn && !IsBusy && HasEnabledSyncPairs;

    public string PauseResumeSyncLabel => IsSyncPaused ? "Resume sync" : "Pause sync";

    public string PauseResumeTrayLabel => IsSyncPaused ? "Resume" : "Pause";

    public bool CanOpenTrayFolder => IsSignedIn && !IsBusy && SyncPairs.Count == 1;

    public string TrayOpenFolderLabel => SyncPairs.Count == 1
        ? "Open " + SyncPairs[0].DisplayName
        : "Open folder";

    public bool IsSyncPaused => HasEnabledSyncPairs
        && SyncPairs
            .Where(static syncPair => syncPair.IsEnabled)
            .All(static syncPair => string.Equals(syncPair.Status, "Paused", StringComparison.Ordinal));

    private bool HasEnabledSyncPairs => SyncPairs.Any(static syncPair => syncPair.IsEnabled);

    public bool IsDashboardVisible => IsSignedIn;

    public bool IsSetupVisible => !IsSignedIn;

    public bool IsServerStepVisible => IsSetupVisible && !IsServerVerified;

    public bool IsSignInStepVisible => IsSetupVisible && IsServerVerified;

    public string SetupTitle => IsServerVerified ? "Sign in" : "Connect Cotton Sync";

    public string SetupSubtitle => IsServerVerified
        ? "Use your Cotton Cloud account."
        : "Choose the Cotton Cloud server for this computer.";

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

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set
        {
            if (SetProperty(ref _enableNotifications, value) && !_isLoadingSnapshot)
            {
                _ = ApplyNotificationsEnabledAsync(value);
            }
        }
    }

    public int ThemeModeIndex
    {
        get => (int)_themeMode;
        set
        {
            AppThemeMode themeMode = NormalizeThemeModeIndex(value);
            if (_themeMode == themeMode)
            {
                return;
            }

            AppThemeMode previousThemeMode = _themeMode;
            _themeMode = themeMode;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeModeLabel));
            _themeService.Apply(themeMode);
            if (!_isLoadingSnapshot)
            {
                _ = ApplyThemeModeAsync(themeMode, previousThemeMode);
            }
        }
    }

    public string ThemeModeLabel => _themeMode switch
    {
        AppThemeMode.System => "System",
        AppThemeMode.Light => "Light",
        AppThemeMode.Dark => "Dark",
        _ => "System",
    };

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
                OnPropertyChanged(nameof(IsDashboardChromeVisible));
            }
        }
    }

    public bool HasLocalFolderSelection => !string.IsNullOrWhiteSpace(LocalFolderPath);

    public bool IsAddSyncPairLocalStepVisible => IsAddSyncPairWizardVisible && !HasLocalFolderSelection;

    public bool IsAddSyncPairCloudStepVisible => IsAddSyncPairWizardVisible && HasLocalFolderSelection;

    public bool IsCreateRemoteFolderVisible
    {
        get => _isCreateRemoteFolderVisible;
        private set
        {
            if (SetProperty(ref _isCreateRemoteFolderVisible, value))
            {
                ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
                CreateRemoteFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set
        {
            if (SetProperty(ref _isSettingsVisible, value))
            {
                OnPropertyChanged(nameof(IsDashboardChromeVisible));
            }
        }
    }

    public int SelectedSettingsTabIndex
    {
        get => _selectedSettingsTabIndex;
        set => SetProperty(ref _selectedSettingsTabIndex, value);
    }

    public string AddSyncPairWizardTitle => HasLocalFolderSelection ? "Choose cloud folder" : "Choose local folder";

    public string AddSyncPairWizardSubtitle => HasLocalFolderSelection
        ? "Pick where this computer folder should sync in Cotton Cloud."
        : "Start with the folder on this computer.";

    public bool IsFutureSyncModesVisible => _featureFlags.ShowFutureSyncModes;

    public string SelectedSyncModeLabel => "Full mirror";

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
                RaiseSetupStateProperties();
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

    public string LastDiagnosticsBundlePath
    {
        get => _lastDiagnosticsBundlePath;
        private set
        {
            if (SetProperty(ref _lastDiagnosticsBundlePath, value))
            {
                OnPropertyChanged(nameof(HasLastDiagnosticsBundlePath));
                OpenDiagnosticsBundleFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasLastDiagnosticsBundlePath => !string.IsNullOrWhiteSpace(LastDiagnosticsBundlePath);

    public string DataDirectory
    {
        get => _dataDirectory;
        private set
        {
            if (SetProperty(ref _dataDirectory, value))
            {
                OnPropertyChanged(nameof(HasDataDirectory));
                OpenDataFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasDataDirectory => !string.IsNullOrWhiteSpace(DataDirectory);

    public string AppDatabasePath
    {
        get => _appDatabasePath;
        private set => SetProperty(ref _appDatabasePath, value);
    }

    public string SyncStateDatabasePath
    {
        get => _syncStateDatabasePath;
        private set => SetProperty(ref _syncStateDatabasePath, value);
    }

    public string TokenStorePath
    {
        get => _tokenStorePath;
        private set => SetProperty(ref _tokenStorePath, value);
    }

    public string NewRemoteFolderName
    {
        get => _newRemoteFolderName;
        set
        {
            if (SetProperty(ref _newRemoteFolderName, value))
            {
                CreateRemoteFolderCommand.RaiseCanExecuteChanged();
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

    public ConflictRowViewModel? SelectedConflict
    {
        get => _selectedConflict;
        set => SetProperty(ref _selectedConflict, value);
    }

    public SyncPairRowViewModel? SelectedSyncPair
    {
        get => _selectedSyncPair;
        set
        {
            SyncPairRowViewModel? previous = _selectedSyncPair;
            if (SetProperty(ref _selectedSyncPair, value))
            {
                if (previous is not null)
                {
                    previous.IsEditorVisible = false;
                }

                UpdateSelectedSyncPairEditorVisibility();
                OnPropertyChanged(nameof(SelectedSyncPairEditableDisplayName));
                OnPropertyChanged(nameof(SelectedSyncPairToggleEnabledLabel));
                if (_pendingRemoveSyncPair is not null && !ReferenceEquals(_pendingRemoveSyncPair, value))
                {
                    ClearRemoveSyncPairConfirmation();
                }

                OpenFolderCommand.RaiseCanExecuteChanged();
                ToggleSelectedSyncPairEnabledCommand.RaiseCanExecuteChanged();
                SaveSelectedSyncPairNameCommand.RaiseCanExecuteChanged();
                RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
                ShowSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
                ConfirmRemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
                CancelRemoveSyncPairCommand.RaiseCanExecuteChanged();
                CancelSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsRemoveSyncPairConfirmationVisible => _pendingRemoveSyncPair is not null;

    public string RemoveSyncPairConfirmationTitle => _pendingRemoveSyncPair is null
        ? "Remove sync folder?"
        : "Remove " + _pendingRemoveSyncPair.DisplayName + "?";

    public string RemoveSyncPairConfirmationPath => _pendingRemoveSyncPair?.LocalPath ?? string.Empty;

    public string SelectedSyncPairEditableDisplayName
    {
        get => SelectedSyncPair?.EditableDisplayName ?? string.Empty;
        set
        {
            if (SelectedSyncPair is { } selected)
            {
                selected.EditableDisplayName = value;
                OnPropertyChanged();
            }
        }
    }

    public string SelectedSyncPairToggleEnabledLabel => SelectedSyncPair?.ToggleEnabledLabel ?? "Enable";

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
        DisposeViewModelResources();
        _controller.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        DisposeViewModelResources();
        await _controller.DisposeAsync().ConfigureAwait(true);
    }

    private void DisposeViewModelResources()
    {
        _controller.StatusChanged -= OnStatusChanged;
        _controller.ActivityReported -= OnActivityReported;
        _controller.TransferProgressChanged -= OnTransferProgressChanged;
        _controller.RunProgressChanged -= OnRunProgressChanged;
        Activities.CollectionChanged -= OnActivitiesChanged;
        Conflicts.CollectionChanged -= OnConflictsChanged;
        SyncPairs.CollectionChanged -= OnSyncPairsChanged;
        RemoteFolders.CollectionChanged -= OnRemoteFoldersChanged;
        SelfTestItems.CollectionChanged -= OnSelfTestItemsChanged;
        Notifications.CollectionChanged -= OnNotificationsChanged;
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
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
            EnableNotifications = snapshot.EnableNotifications;
            ThemeModeIndex = (int)snapshot.ThemeMode;
            DataDirectory = snapshot.DataPaths.DataDirectory;
            AppDatabasePath = snapshot.DataPaths.AppDatabasePath;
            SyncStateDatabasePath = snapshot.DataPaths.SyncStateDatabasePath;
            TokenStorePath = snapshot.DataPaths.TokenStorePath;
            SyncPairs.Clear();
            foreach (DesktopSyncPairSnapshot syncPair in snapshot.SyncPairs)
            {
                SyncPairs.Add(ToRow(syncPair));
            }

            SelectedSyncPair = SyncPairs.FirstOrDefault();
            IsSignedIn = snapshot.IsSignedIn;
            AccountName = snapshot.IsSignedIn
                ? ResolveAccountDisplayName(snapshot.AccountName, snapshot.RememberedUsername)
                : "Signed out";
            GlobalStatus = snapshot.IsSignedIn
                ? "Connected"
                : SyncPairs.Count == 0 ? "Ready to connect" : "Ready";
            RefreshCurrentProgressText();
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

    internal async Task ApplyVisualSmokeScenarioAsync(DesktopVisualSmokeScenario? scenario)
    {
        if (scenario is null)
        {
            return;
        }

        switch (scenario)
        {
            case DesktopVisualSmokeScenario.SignInError:
                ServerUrl = "https://app.cottoncloud.dev/";
                IsServerProbeChecking = false;
                IsServerProbeFailed = false;
                IsServerVerified = true;
                ServerProbeStatus = "Cotton Cloud verified";
                Username = string.IsNullOrWhiteSpace(Username) ? "qa@cottoncloud.dev" : Username;
                Password = "wrong-password";
                TotpCode = "000000";
                GlobalStatus = "Sign-in failed";
                ActionRequiredMessage = "Invalid username or password.";
                break;
            case DesktopVisualSmokeScenario.AddFolder:
                LocalFolderPath = CreateVisualSmokeLocalRootPath();
                IsAddSyncPairWizardVisible = true;
                await LoadRemoteFoldersAsync("/").ConfigureAwait(true);
                break;
            case DesktopVisualSmokeScenario.EmptyDashboard:
                break;
            case DesktopVisualSmokeScenario.Settings:
                SelectedSettingsTabIndex = 0;
                await ShowSettingsAsync().ConfigureAwait(true);
                break;
            case DesktopVisualSmokeScenario.SettingsDiagnostics:
                SelectedSettingsTabIndex = 3;
                await ShowSettingsAsync().ConfigureAwait(true);
                await SelfTestAsync().ConfigureAwait(true);
                await ExportDiagnosticsAsync().ConfigureAwait(true);
                break;
            case DesktopVisualSmokeScenario.Error:
                GlobalStatus = "Action required";
                ActionRequiredMessage = DesktopActionRequiredMessageResolver.MissingDesktopSyncChangesApiMessage;
                AddActivity("Error", SelectedSyncPair?.LocalPath ?? string.Empty, ActionRequiredMessage);
                break;
            case DesktopVisualSmokeScenario.Progress:
                ApplyVisualSmokeProgressScenario();
                break;
            case DesktopVisualSmokeScenario.FolderControls:
                if (SyncPairs.FirstOrDefault() is { } syncPair)
                {
                    await ShowSelectedSyncPairEditorAsync(syncPair).ConfigureAwait(true);
                }

                break;
            case DesktopVisualSmokeScenario.Conflict:
                AddActivity("Conflict", "Reports/budget.xlsx", "Local and cloud versions changed at the same time.");
                AddConflict(
                    SelectedSyncPair?.Id,
                    "Reports/budget.xlsx",
                    "Local and cloud versions changed at the same time.",
                    DateTimeOffset.Now);
                break;
            case DesktopVisualSmokeScenario.Dashboard:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        RefreshCurrentProgressText();
        RefreshDiagnosticsItems();
        RaiseCommandStates();
    }

    private static string CreateVisualSmokeLocalRootPath()
    {
        return OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Cotton")
            : "/home/qa/Cotton";
    }

    private void ApplyVisualSmokeProgressScenario()
    {
        SyncPairRowViewModel? syncPair = SyncPairs.FirstOrDefault();
        if (syncPair is null)
        {
            return;
        }

        DateTime startedAtUtc = new(2026, 6, 4, 9, 15, 0, DateTimeKind.Utc);
        GlobalStatus = "Syncing";
        syncPair.Status = "Syncing";
        ApplyRunProgress(new DesktopRunProgressSnapshot(
            syncPair.Id,
            SyncRunProgressStage.ReconcilingFiles,
            FilesCompleted: 8,
            FilesTotal: 31,
            CurrentPath: "Reports/quarterly-budget.xlsx",
            startedAtUtc,
            IsCompleted: false,
            startedAtUtc.AddSeconds(2)));
        ApplyTransferProgress(new DesktopTransferProgressSnapshot(
            syncPair.Id,
            SyncTransferDirection.Upload,
            "Reports/quarterly-budget.xlsx",
            TransferredBytes: 0,
            TotalBytes: 25_165_824,
            IsCompleted: false,
            startedAtUtc));
        ApplyTransferProgress(new DesktopTransferProgressSnapshot(
            syncPair.Id,
            SyncTransferDirection.Upload,
            "Reports/quarterly-budget.xlsx",
            TransferredBytes: 6_291_456,
            TotalBytes: 25_165_824,
            IsCompleted: false,
            startedAtUtc.AddSeconds(2),
            SpeedBytesPerSecond: 3_145_728,
            EstimatedTimeRemaining: TimeSpan.FromSeconds(6)));
        AddActivity("Upload", "Reports/quarterly-budget.xlsx", "Uploading quarterly-budget.xlsx");
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

    private async Task ApplyNotificationsEnabledAsync(bool enabled)
    {
        if (_isApplyingNotificationPreference)
        {
            return;
        }

        _isApplyingNotificationPreference = true;
        try
        {
            await _controller.SetNotificationsEnabledAsync(enabled).ConfigureAwait(true);
            AddActivity("Settings", string.Empty, enabled ? "Desktop notifications enabled" : "Desktop notifications disabled");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _isLoadingSnapshot = true;
            EnableNotifications = !enabled;
            _isLoadingSnapshot = false;
            HandleCommandError(exception);
        }
        finally
        {
            _isApplyingNotificationPreference = false;
        }
    }

    private async Task ApplyThemeModeAsync(AppThemeMode themeMode, AppThemeMode previousThemeMode)
    {
        if (_isApplyingThemePreference)
        {
            return;
        }

        _isApplyingThemePreference = true;
        try
        {
            await _controller.SetThemeModeAsync(themeMode).ConfigureAwait(true);
            AddActivity("Settings", string.Empty, "Theme set to " + ThemeModeLabel);
            RefreshDiagnosticsItems();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _themeMode = previousThemeMode;
            OnPropertyChanged(nameof(ThemeModeIndex));
            OnPropertyChanged(nameof(ThemeModeLabel));
            _themeService.Apply(previousThemeMode);
            HandleCommandError(exception);
        }
        finally
        {
            _isApplyingThemePreference = false;
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
            IsSelectedSyncPairEditorVisible = false;
            GlobalStatus = "Sync requested";
            RefreshCurrentProgressText();
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

        string? overlapMessage = GetLocalFolderOverlapMessage(selectedPath);
        if (overlapMessage is not null)
        {
            LocalFolderPath = string.Empty;
            NewRemoteFolderName = string.Empty;
            IsCreateRemoteFolderVisible = false;
            ResetRemoteFolderSelection();
            RemoteFolders.Clear();
            _isLocalFolderSelectionError = true;
            GlobalStatus = "Action required";
            ActionRequiredMessage = overlapMessage;
            AddActivity("Warning", selectedPath, ActionRequiredMessage);
            RefreshCurrentProgressText();
            return;
        }

        LocalFolderPath = selectedPath;
        ClearLocalFolderSelectionError();
        AddActivity("Folder", selectedPath, "Local folder selected");
        if (IsAddSyncPairWizardVisible)
        {
            NewRemoteFolderName = string.Empty;
            IsCreateRemoteFolderVisible = false;
            await LoadRemoteFoldersAsync("/").ConfigureAwait(true);
        }
    }

    private Task CancelAddSyncPairAsync()
    {
        LocalFolderPath = string.Empty;
        NewRemoteFolderName = string.Empty;
        IsCreateRemoteFolderVisible = false;
        ResetRemoteFolderSelection();
        ClearLocalFolderSelectionError();
        IsAddSyncPairWizardVisible = false;
        return Task.CompletedTask;
    }

    private Task ChangeServerAsync()
    {
        Password = string.Empty;
        TotpCode = string.Empty;
        SetDesktopSyncChangesApiUnavailable(false);
        IsServerVerified = false;
        IsServerProbeFailed = false;
        ServerProbeStatus = "Edit server address";
        return Task.CompletedTask;
    }

    private bool CanGoUpRemoteFolder()
    {
        return !IsBusy
            && CanUseAddSyncPairFlow
            && IsAddSyncPairWizardVisible
            && RemoteBrowserPath != "/";
    }

    private async Task OpenFolderAsync(object? parameter)
    {
        SyncPairRowViewModel? selected = ResolveOpenFolderTarget(parameter);
        if (selected is null)
        {
            return;
        }

        await _controller.OpenFolderAsync(selected.LocalPath).ConfigureAwait(true);
        AddActivity("Open", selected.LocalPath, "Folder opened");
    }

    private Task OpenTrayFolderAsync()
    {
        return SyncPairs.Count == 1
            ? OpenFolderAsync(SyncPairs[0])
            : Task.CompletedTask;
    }

    private SyncPairRowViewModel? ResolveOpenFolderTarget(object? parameter)
    {
        return parameter as SyncPairRowViewModel ?? SelectedSyncPair;
    }

    private SyncPairRowViewModel? ResolveSyncPairTarget(object? parameter)
    {
        return parameter as SyncPairRowViewModel ?? SelectedSyncPair;
    }

    private async Task OpenConflictAsync(object? parameter)
    {
        if (parameter is not ConflictRowViewModel conflict)
        {
            return;
        }

        SyncPairRowViewModel? syncPair = ResolveConflictSyncPair(conflict);
        if (syncPair is null)
        {
            GlobalStatus = "Action required";
            ActionRequiredMessage = "Sync folder for conflict was not found.";
            AddActivity("Warning", conflict.Path, "Sync folder for conflict was not found");
            return;
        }

        string openPath = ResolveConflictOpenPath(syncPair.LocalPath, conflict.Path);
        await _controller.OpenFolderAsync(openPath).ConfigureAwait(true);
        ActionRequiredMessage = string.Empty;
        AddActivity("Open", openPath, "Conflict location opened");
    }

    private async Task OpenWebAsync()
    {
        await _controller.OpenWebAsync().ConfigureAwait(true);
        AddActivity("Open", string.Empty, "Cotton Cloud opened");
    }

    private async Task ToggleSelectedSyncPairEnabledAsync()
    {
        SyncPairRowViewModel? selected = SelectedSyncPair;
        if (selected is null)
        {
            return;
        }

        bool enabled = !selected.IsEnabled;
        IsBusy = true;
        try
        {
            await _controller.SetSyncPairEnabledAsync(selected.Id, enabled).ConfigureAwait(true);
            selected.IsEnabled = enabled;
            OnPropertyChanged(nameof(SelectedSyncPairToggleEnabledLabel));
            selected.Status = enabled ? "Idle" : "Disabled";
            selected.CurrentOperation = string.Empty;
            GlobalStatus = enabled ? "Ready" : "Folder disabled";
            ActionRequiredMessage = string.Empty;
            AddActivity("Pair", selected.LocalPath, enabled ? "Folder enabled" : "Folder disabled");
            RefreshCurrentProgressText();
            RefreshDiagnosticsItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSelectedSyncPairNameAsync()
    {
        SyncPairRowViewModel? selected = SelectedSyncPair;
        if (selected is null)
        {
            return;
        }

        string displayName = selected.EditableDisplayName.Trim();
        if (displayName.Length == 0)
        {
            GlobalStatus = "Action required";
            ActionRequiredMessage = "Sync folder name is required.";
            AddActivity("Warning", selected.LocalPath, "Sync folder name is required");
            return;
        }

        IsBusy = true;
        try
        {
            await _controller.RenameSyncPairAsync(selected.Id, displayName).ConfigureAwait(true);
            selected.DisplayName = displayName;
            selected.EditableDisplayName = displayName;
            OnPropertyChanged(nameof(SelectedSyncPairEditableDisplayName));
            RaiseTrayOpenFolderState();
            GlobalStatus = "Folder renamed";
            ActionRequiredMessage = string.Empty;
            AddActivity("Pair", selected.LocalPath, "Sync folder renamed to " + displayName);
            RefreshDiagnosticsItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task RequestRemoveSelectedSyncPairAsync()
    {
        SyncPairRowViewModel? selected = SelectedSyncPair;
        if (selected is not null)
        {
            IsSelectedSyncPairEditorVisible = true;
            SetPendingRemoveSyncPair(selected);
        }

        return Task.CompletedTask;
    }

    private Task CancelRemoveSyncPairAsync()
    {
        ClearRemoveSyncPairConfirmation();
        return Task.CompletedTask;
    }

    private Task ShowSelectedSyncPairEditorAsync(object? parameter)
    {
        SyncPairRowViewModel? target = ResolveSyncPairTarget(parameter);
        if (target is null)
        {
            return Task.CompletedTask;
        }

        SelectedSyncPair = target;
        ClearRemoveSyncPairConfirmation();
        IsSelectedSyncPairEditorVisible = true;
        return Task.CompletedTask;
    }

    private Task CancelSelectedSyncPairEditorAsync()
    {
        ClearRemoveSyncPairConfirmation();
        IsSelectedSyncPairEditorVisible = false;
        return Task.CompletedTask;
    }

    private async Task ConfirmRemoveSelectedSyncPairAsync()
    {
        SyncPairRowViewModel? selected = _pendingRemoveSyncPair;
        if (selected is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _controller.RemoveSyncPairAsync(selected.Id).ConfigureAwait(true);
            int removedIndex = SyncPairs.IndexOf(selected);
            SyncPairs.Remove(selected);
            ClearRemoveSyncPairConfirmation();
            IsSelectedSyncPairEditorVisible = false;
            SelectedSyncPair = SyncPairs.Count == 0
                ? null
                : SyncPairs[Math.Clamp(removedIndex, 0, SyncPairs.Count - 1)];
            GlobalStatus = SyncPairs.Count == 0 ? "Ready to add a folder" : "Ready";
            ActionRequiredMessage = string.Empty;
            AddActivity("Pair", selected.LocalPath, "Sync folder removed");
            RefreshCurrentProgressText();
            RefreshDiagnosticsItems();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetPendingRemoveSyncPair(SyncPairRowViewModel? syncPair)
    {
        if (ReferenceEquals(_pendingRemoveSyncPair, syncPair))
        {
            return;
        }

        _pendingRemoveSyncPair = syncPair;
        OnPropertyChanged(nameof(IsRemoveSyncPairConfirmationVisible));
        OnPropertyChanged(nameof(RemoveSyncPairConfirmationTitle));
        OnPropertyChanged(nameof(RemoveSyncPairConfirmationPath));
        RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
        ConfirmRemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
        CancelRemoveSyncPairCommand.RaiseCanExecuteChanged();
    }

    private void ClearRemoveSyncPairConfirmation()
    {
        SetPendingRemoveSyncPair(null);
    }

    private void UpdateSelectedSyncPairEditorVisibility()
    {
        if (SelectedSyncPair is { } selected)
        {
            selected.IsEditorVisible = IsSelectedSyncPairEditorVisible;
        }
    }

    private async Task PauseAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.PauseAllAsync().ConfigureAwait(true);
            GlobalStatus = "Paused";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Paused", enabledOnly: true);
            RefreshCurrentProgressText();
            AddActivity("Sync", string.Empty, "Synchronization paused");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task PauseResumeAsync()
    {
        return IsSyncPaused ? ResumeAsync() : PauseAsync();
    }

    private async Task ResumeAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.ResumeAllAsync().ConfigureAwait(true);
            GlobalStatus = "Ready";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Idle", enabledOnly: true);
            RefreshCurrentProgressText();
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
            AccountName = ResolveAccountDisplayName(session.Email, session.Username);
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
            TotpCode = string.Empty;
            IsAddSyncPairWizardVisible = false;
            IsSettingsVisible = false;
            IsSelectedSyncPairEditorVisible = false;
            ActionRequiredMessage = string.Empty;
            Notifications.Clear();
            _notificationTracker.Reset();
            RemoteFolders.Clear();
            ClearRunProgress();
            ClearTransferProgress();
            SetAllPairStatuses("Idle");
            RefreshCurrentProgressText();
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
            SetAllPairStatuses("Sync requested", "Waiting to sync changes", enabledOnly: true);
            RefreshCurrentProgressText();
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
            string actionRequiredMessage = DesktopActionRequiredMessageResolver.FromSelfTest(result);
            SetDesktopSyncChangesApiUnavailable(HasMissingDesktopSyncChangesApiFailure(result));
            GlobalStatus = result.Passed ? "Self-test passed" : "Action required";
            ActionRequiredMessage = actionRequiredMessage;
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
        bool preserveActionRequired = HasActionRequired;
        IsBusy = true;
        try
        {
            string bundlePath = await _controller.ExportDiagnosticsAsync().ConfigureAwait(true);
            LastDiagnosticsBundlePath = bundlePath;
            if (!preserveActionRequired)
            {
                GlobalStatus = "Diagnostics exported";
                ActionRequiredMessage = string.Empty;
            }

            AddActivity("Diagnostics", bundlePath, "Diagnostics bundle exported to " + bundlePath);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenDataFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(DataDirectory))
        {
            return;
        }

        await _controller.OpenFolderAsync(DataDirectory).ConfigureAwait(true);
        AddActivity("Open", DataDirectory, "Data folder opened");
    }

    private async Task OpenDiagnosticsBundleFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(LastDiagnosticsBundlePath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(LastDiagnosticsBundlePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        await _controller.OpenFolderAsync(directory).ConfigureAwait(true);
        AddActivity("Open", directory, "Diagnostics folder opened");
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
        NewRemoteFolderName = string.Empty;
        IsCreateRemoteFolderVisible = false;

        if (HasLocalFolderSelection && string.IsNullOrWhiteSpace(RemoteFolderPath))
        {
            await LoadRemoteFoldersAsync("/").ConfigureAwait(true);
        }
    }

    private Task ShowCreateRemoteFolderAsync()
    {
        IsCreateRemoteFolderVisible = true;
        NewRemoteFolderName = string.Empty;
        return Task.CompletedTask;
    }

    private Task CancelCreateRemoteFolderAsync()
    {
        NewRemoteFolderName = string.Empty;
        IsCreateRemoteFolderVisible = false;
        return Task.CompletedTask;
    }

    private async Task CreateRemoteFolderAsync()
    {
        string folderName = NewRemoteFolderName.Trim();
        if (folderName.Length == 0)
        {
            GlobalStatus = "Action required";
            ActionRequiredMessage = "Cloud folder name is required.";
            AddActivity("Warning", RemoteBrowserPath, "Cloud folder name is required");
            return;
        }

        IsBusy = true;
        try
        {
            DesktopRemoteFolderSnapshot folder = await _controller
                .CreateRemoteFolderAsync(RemoteBrowserPath, folderName)
                .ConfigureAwait(true);
            NewRemoteFolderName = string.Empty;
            IsCreateRemoteFolderVisible = false;
            await LoadRemoteFoldersAsync(folder.Path).ConfigureAwait(true);
            GlobalStatus = "Cloud folder created";
            ActionRequiredMessage = string.Empty;
            AddActivity("Cloud", folder.Path, "Cloud folder created");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanAddSyncPair()
    {
        return !IsBusy
            && CanUseAddSyncPairFlow
            && IsSignedIn
            && !string.IsNullOrWhiteSpace(LocalFolderPath)
            && !string.IsNullOrWhiteSpace(RemoteFolderPath);
    }

    private bool CanBrowseLocalFolder()
    {
        return !IsBusy && CanUseAddSyncPairFlow;
    }

    private bool CanOpenRemoteFolder()
    {
        return !IsBusy
            && CanUseAddSyncPairFlow
            && SelectedRemoteFolder is not null;
    }

    private bool CanShowAddSyncPair()
    {
        return IsSignedIn
            && !IsBusy
            && CanUseAddSyncPairFlow;
    }

    private bool CanShowCreateRemoteFolder()
    {
        return !IsBusy
            && CanUseAddSyncPairFlow
            && IsAddSyncPairCloudStepVisible;
    }

    private string? GetLocalFolderOverlapMessage(string localPath)
    {
        if (SyncPairs.Count == 0)
        {
            return null;
        }

        Guid candidateId = Guid.NewGuid();
        List<SyncPairSettings> syncPairs = SyncPairs
            .Select(ToSettingsForValidation)
            .Append(new SyncPairSettings
            {
                Id = candidateId,
                DisplayName = "Candidate",
                LocalRootPath = localPath,
                RemoteRootNodeId = Guid.NewGuid(),
                RemoteDisplayPath = "/",
                IsEnabled = true,
                Mode = SyncPairMode.FullMirror,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            })
            .ToList();
        return _syncPairSettingsValidator
            .Validate(syncPairs)
            .Errors
            .FirstOrDefault(error => error.Issue == SyncPairValidationIssue.OverlappingLocalRoots
                && (error.SyncPairId == candidateId || error.OtherSyncPairId == candidateId))
            ?.Message;
    }

    private void ClearLocalFolderSelectionError()
    {
        if (!_isLocalFolderSelectionError)
        {
            return;
        }

        _isLocalFolderSelectionError = false;
        ActionRequiredMessage = string.Empty;
        if (IsSignedIn)
        {
            GlobalStatus = "Connected";
        }
    }

    private bool CanCreateRemoteFolder()
    {
        return !IsBusy
            && CanUseAddSyncPairFlow
            && IsSignedIn
            && IsAddSyncPairCloudStepVisible
            && !string.IsNullOrWhiteSpace(NewRemoteFolderName);
    }

    private bool CanUseAddSyncPairFlow => !_isDesktopSyncChangesApiUnavailable;

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
        GlobalStatus = ResolveCommandFailureStatus();
        string actionRequiredMessage = DesktopActionRequiredMessageResolver.FromException(exception);
        ActionRequiredMessage = actionRequiredMessage;
        AddActivity("Error", string.Empty, actionRequiredMessage);
        RefreshCurrentProgressText();
        IsBusy = false;
    }

    private string ResolveCommandFailureStatus()
    {
        if (IsSignedIn)
        {
            return "Action required";
        }

        return IsSignInStepVisible ? "Sign-in failed" : "Action required";
    }

    private async Task ProbeServerAfterDelayAsync(string serverUrl, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
            DesktopServerProbeResult result = await _controller.ProbeServerAsync(serverUrl, cancellationToken)
                .ConfigureAwait(false);
            await _uiDispatcher.InvokeAsync(
                () => ApplyServerProbeResult(result),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Failed to probe Cotton server {0}: {1}", serverUrl, exception);
            await _uiDispatcher.InvokeAsync(
                ApplyServerProbeFailure,
                CancellationToken.None).ConfigureAwait(false);
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
        if (result.IsCottonServer)
        {
            SetDesktopSyncChangesApiUnavailable(false);
            ApplyNormalizedServerUrl(result.ServerUrl);
        }

        IsServerVerified = result.IsCottonServer;
        IsServerProbeFailed = !result.IsCottonServer;
        ServerProbeStatus = result.IsCottonServer
            ? "Cotton Cloud"
            : "Cotton server not found";
    }

    private void ApplyNormalizedServerUrl(Uri serverUrl)
    {
        if (SetProperty(ref _serverUrl, serverUrl.AbsoluteUri, nameof(ServerUrl)))
        {
            SignInCommand.RaiseCanExecuteChanged();
            RefreshDiagnosticsItems();
        }
    }

    private void ResetServerProbe()
    {
        SetDesktopSyncChangesApiUnavailable(false);
        IsServerProbeChecking = false;
        IsServerVerified = false;
        IsServerProbeFailed = false;
        ServerProbeStatus = string.Empty;
    }

    private void SetDesktopSyncChangesApiUnavailable(bool isUnavailable)
    {
        if (_isDesktopSyncChangesApiUnavailable == isUnavailable)
        {
            return;
        }

        _isDesktopSyncChangesApiUnavailable = isUnavailable;
        RaiseAddSyncPairFlowCommandStates();
    }

    private static bool IsMissingDesktopSyncChangesApiMessage(string message)
    {
        return DesktopActionRequiredMessageResolver.IsMissingDesktopSyncChangesApi(message);
    }

    private static bool HasMissingDesktopSyncChangesApiFailure(DesktopSelfTestSnapshot selfTest)
    {
        return selfTest.Items.Any(static item => DesktopActionRequiredMessageResolver.IsMissingDesktopSyncChangesApi(item.Details));
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
        OnPropertyChanged(nameof(IsStatusCardVisible));
        RaiseSyncStateProperties();
        OpenFolderCommand.RaiseCanExecuteChanged();
        RaiseTrayOpenFolderState();
        ToggleSelectedSyncPairEnabledCommand.RaiseCanExecuteChanged();
        SaveSelectedSyncPairNameCommand.RaiseCanExecuteChanged();
        RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
        ShowSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
        RefreshCurrentProgressText();
        RefreshDiagnosticsItems();
    }

    private void OnActivitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoActivities));
        OnPropertyChanged(nameof(HasActivities));
    }

    private void OnConflictsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(HasStatusAttention));
        OnPropertyChanged(nameof(IsStatusCardVisible));
        OnPropertyChanged(nameof(ConflictCountLabel));
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(StatusCardTitle));
        OnPropertyChanged(nameof(StatusCardDetailText));
        OnPropertyChanged(nameof(HasStatusCardDetail));
        RefreshCurrentProgressText();
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
            NewRemoteFolderName = string.Empty;
            IsCreateRemoteFolderVisible = false;
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

            SelectedRemoteFolder = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetRemoteFolderSelection()
    {
        RemoteBrowserPath = "/";
        RemoteFolderPath = string.Empty;
        SelectedRemoteFolder = null;
        NewRemoteFolderName = string.Empty;
        IsCreateRemoteFolderVisible = false;
        RemoteFolders.Clear();
    }

    private void OnStatusChanged(object? sender, DesktopSyncStatusSnapshot status)
    {
        _uiDispatcher.Post(() => ApplyStatus(status));
    }

    private void OnActivityReported(object? sender, DesktopActivitySnapshot activity)
    {
        if (_uiDispatcher.CheckAccess())
        {
            ApplyActivity(activity);
            return;
        }

        _uiDispatcher.Post(() => ApplyActivity(activity));
    }

    private void OnTransferProgressChanged(object? sender, DesktopTransferProgressSnapshot progress)
    {
        if (_uiDispatcher.CheckAccess())
        {
            ApplyTransferProgress(progress);
            return;
        }

        _uiDispatcher.Post(() => ApplyTransferProgress(progress));
    }

    private void OnRunProgressChanged(object? sender, DesktopRunProgressSnapshot progress)
    {
        if (_uiDispatcher.CheckAccess())
        {
            ApplyRunProgress(progress);
            return;
        }

        _uiDispatcher.Post(() => ApplyRunProgress(progress));
    }

    private void ApplyActivity(DesktopActivitySnapshot activity)
    {
        DateTimeOffset occurredAt = new DateTimeOffset(DateTime.SpecifyKind(activity.OccurredAtUtc, DateTimeKind.Utc))
            .ToLocalTime();
        AddActivity(
            activity.Kind,
            activity.Path,
            activity.Details,
            occurredAt);
        if (string.Equals(activity.Kind, "Conflict", StringComparison.Ordinal))
        {
            AddConflict(activity.SyncPairId, activity.Path, activity.Details, occurredAt);
        }
    }

    private void ApplyTransferProgress(DesktopTransferProgressSnapshot progress)
    {
        SyncPairRowViewModel? syncPair = SyncPairs.FirstOrDefault(pair => pair.Id == progress.SyncPairId);
        if (syncPair is null || progress.Direction == SyncTransferDirection.Unknown)
        {
            return;
        }

        bool isNewTransfer = _transferSyncPairId != progress.SyncPairId
            || _transferDirection != progress.Direction
            || !string.Equals(_transferRelativePath, progress.RelativePath, StringComparison.Ordinal);
        if (isNewTransfer)
        {
            _transferSyncPairId = progress.SyncPairId;
            _transferDirection = progress.Direction;
            _transferRelativePath = progress.RelativePath;
        }

        HasCurrentTransfer = true;
        IsCurrentTransferIndeterminate = !progress.TotalBytes.HasValue && !progress.IsCompleted;
        CurrentTransferProgressValue = CalculateProgressValue(progress);
        CurrentTransferTitle = CreateTransferTitle(progress, syncPair.DisplayName);
        CurrentTransferDetails = CreateTransferDetails(progress);
        syncPair.CurrentOperation = CreateTransferOperation(progress);
        syncPair.HasCurrentProgress = true;
        syncPair.IsCurrentProgressIndeterminate = IsCurrentTransferIndeterminate;
        syncPair.CurrentProgressValue = CurrentTransferProgressValue;
        RaiseCurrentWorkProgressProperties();
        RefreshCurrentProgressText();
    }

    private void ApplyRunProgress(DesktopRunProgressSnapshot progress)
    {
        SyncPairRowViewModel? syncPair = SyncPairs.FirstOrDefault(pair => pair.Id == progress.SyncPairId);
        if (syncPair is null || progress.Stage == SyncRunProgressStage.Unknown)
        {
            return;
        }

        HasCurrentRunProgress = true;
        IsCurrentRunProgressIndeterminate = !progress.FilesTotal.HasValue && !progress.IsCompleted;
        CurrentRunProgressValue = CalculateRunProgressValue(progress);
        CurrentRunProgressTitle = CreateRunProgressTitle(progress, syncPair.DisplayName);
        CurrentRunProgressDetails = CreateRunProgressDetails(progress);
        if (!HasCurrentTransfer || _transferSyncPairId != progress.SyncPairId)
        {
            syncPair.CurrentOperation = CreateRunProgressOperation(progress);
            syncPair.HasCurrentProgress = true;
            syncPair.IsCurrentProgressIndeterminate = IsCurrentRunProgressIndeterminate;
            syncPair.CurrentProgressValue = CurrentRunProgressValue;
        }

        RaiseCurrentWorkProgressProperties();
        RefreshCurrentProgressText();
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
            row.IsEnabled = !string.Equals(pairStatus.Status, "Disabled", StringComparison.Ordinal);
            row.LastError = pairStatus.LastError;
            row.CurrentOperation = pairStatus.CurrentOperation ?? string.Empty;
            if (IsActiveSyncStatus(pairStatus))
            {
                EnsureSyncPairProgress(row);
            }
            else
            {
                ClearSyncPairProgress(row);
            }

            if (pairStatus.LastSyncedAtUtc.HasValue)
            {
                row.LastSyncedAtUtc = pairStatus.LastSyncedAtUtc;
            }

            if (ShouldAddStatusErrorActivity(pairStatus))
            {
                string rawError = pairStatus.LastError ?? string.Empty;
                string activityMessage = DesktopActionRequiredMessageResolver.FromSyncPairStatus(pairStatus);
                AddActivity(
                    "Error",
                    row.LocalPath,
                    string.IsNullOrWhiteSpace(activityMessage) ? rawError : activityMessage);
            }
        }

        GlobalStatus = ResolveGlobalStatus(status);
        ActionRequiredMessage = DesktopActionRequiredMessageResolver.FromStatus(status);
        if (!status.SyncPairs.Any(IsActiveSyncStatus))
        {
            ClearTransferProgress();
            ClearRunProgress();
        }

        RaiseSyncStateProperties();
        RefreshCurrentProgressText();
        AddNotifications(_notificationTracker.Apply(status, SyncPairs.ToDictionary(static pair => pair.Id, static pair => pair.DisplayName)));
        RefreshDiagnosticsItems();
    }

    private bool ShouldAddStatusErrorActivity(DesktopSyncPairStatusSnapshot pairStatus)
    {
        if (string.IsNullOrWhiteSpace(pairStatus.LastError))
        {
            _lastStatusErrorActivityMessages.Remove(pairStatus.Id);
            return false;
        }

        if (_lastStatusErrorActivityMessages.TryGetValue(pairStatus.Id, out string? lastError)
            && string.Equals(lastError, pairStatus.LastError, StringComparison.Ordinal))
        {
            return false;
        }

        _lastStatusErrorActivityMessages[pairStatus.Id] = pairStatus.LastError;
        return true;
    }

    private static void EnsureSyncPairProgress(SyncPairRowViewModel row)
    {
        if (row.HasCurrentProgress)
        {
            return;
        }

        row.HasCurrentProgress = true;
        row.IsCurrentProgressIndeterminate = true;
        row.CurrentProgressValue = 0;
    }

    private static void ClearSyncPairProgress(SyncPairRowViewModel row)
    {
        row.HasCurrentProgress = false;
        row.IsCurrentProgressIndeterminate = false;
        row.CurrentProgressValue = 0;
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
            if (EnableNotifications)
            {
                _notificationService.Show(request.Title, request.Message);
            }
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

        IEnumerable<DesktopSyncPairStatusSnapshot> enabledPairs = status.SyncPairs
            .Where(static pair => !string.Equals(pair.Status, "Disabled", StringComparison.Ordinal));
        if (enabledPairs.Any()
            && enabledPairs.All(static pair => string.Equals(pair.Status, "Paused", StringComparison.Ordinal)))
        {
            return "Paused";
        }

        return "Connected";
    }

    private void AddActivity(string kind, string path, string details)
    {
        AddActivity(kind, path, details, DateTimeOffset.Now);
    }

    private void AddActivity(string kind, string path, string details, DateTimeOffset occurredAt)
    {
        Activities.Insert(0, new ActivityRowViewModel
        {
            Time = occurredAt.ToString("HH:mm", CultureInfo.CurrentCulture),
            Kind = kind,
            Path = path,
            Details = details,
        });
        while (Activities.Count > MaxActivityRows)
        {
            Activities.RemoveAt(Activities.Count - 1);
        }
    }

    private void AddConflict(Guid? syncPairId, string path, string details, DateTimeOffset occurredAt)
    {
        var conflict = new ConflictRowViewModel
        {
            SyncPairId = syncPairId,
            Time = occurredAt.ToString("HH:mm", CultureInfo.CurrentCulture),
            Path = path,
            Details = details,
        };
        Conflicts.Insert(0, conflict);
        SelectedConflict ??= conflict;
        while (Conflicts.Count > MaxConflictRows)
        {
            Conflicts.RemoveAt(Conflicts.Count - 1);
        }
    }

    private void RaiseCommandStates()
    {
        RaiseSyncStateProperties();
        SignInCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
        AddSyncPairCommand.RaiseCanExecuteChanged();
        BrowseLocalFolderCommand.RaiseCanExecuteChanged();
        CancelAddSyncPairCommand.RaiseCanExecuteChanged();
        CancelCreateRemoteFolderCommand.RaiseCanExecuteChanged();
        CancelRemoveSyncPairCommand.RaiseCanExecuteChanged();
        ChangeServerCommand.RaiseCanExecuteChanged();
        CreateRemoteFolderCommand.RaiseCanExecuteChanged();
        OpenRemoteFolderCommand.RaiseCanExecuteChanged();
        RemoteFolderUpCommand.RaiseCanExecuteChanged();
        SyncNowCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        PauseResumeCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
        OpenTrayFolderCommand.RaiseCanExecuteChanged();
        OpenConflictCommand.RaiseCanExecuteChanged();
        ToggleSelectedSyncPairEnabledCommand.RaiseCanExecuteChanged();
        SaveSelectedSyncPairNameCommand.RaiseCanExecuteChanged();
        RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
        ShowSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
        ConfirmRemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
        CancelSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
        OpenWebCommand.RaiseCanExecuteChanged();
        ShowAddSyncPairCommand.RaiseCanExecuteChanged();
        ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
        ShowSettingsCommand.RaiseCanExecuteChanged();
        CloseSettingsCommand.RaiseCanExecuteChanged();
        SelfTestCommand.RaiseCanExecuteChanged();
        ExportDiagnosticsCommand.RaiseCanExecuteChanged();
        OpenDataFolderCommand.RaiseCanExecuteChanged();
        OpenDiagnosticsBundleFolderCommand.RaiseCanExecuteChanged();
        RaiseTrayOpenFolderProperties();
    }

    private void RaiseSyncStateProperties()
    {
        OnPropertyChanged(nameof(CanSyncNow));
        OnPropertyChanged(nameof(CanPauseSync));
        OnPropertyChanged(nameof(CanResumeSync));
        OnPropertyChanged(nameof(CanTogglePauseResumeSync));
        OnPropertyChanged(nameof(PauseResumeSyncLabel));
        OnPropertyChanged(nameof(PauseResumeTrayLabel));
        OnPropertyChanged(nameof(IsSyncPaused));
    }

    private void RaiseTrayOpenFolderState()
    {
        OpenTrayFolderCommand.RaiseCanExecuteChanged();
        RaiseTrayOpenFolderProperties();
    }

    private void RaiseTrayOpenFolderProperties()
    {
        OnPropertyChanged(nameof(CanOpenTrayFolder));
        OnPropertyChanged(nameof(TrayOpenFolderLabel));
    }

    private void RaiseAddSyncPairFlowCommandStates()
    {
        AddSyncPairCommand.RaiseCanExecuteChanged();
        BrowseLocalFolderCommand.RaiseCanExecuteChanged();
        CreateRemoteFolderCommand.RaiseCanExecuteChanged();
        OpenRemoteFolderCommand.RaiseCanExecuteChanged();
        RemoteFolderUpCommand.RaiseCanExecuteChanged();
        ShowAddSyncPairCommand.RaiseCanExecuteChanged();
        ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
    }

    private void RaiseSetupStateProperties()
    {
        OnPropertyChanged(nameof(IsServerStepVisible));
        OnPropertyChanged(nameof(IsSignInStepVisible));
        OnPropertyChanged(nameof(SetupTitle));
        OnPropertyChanged(nameof(SetupSubtitle));
    }

    private void RaiseWizardStateProperties()
    {
        OnPropertyChanged(nameof(HasLocalFolderSelection));
        OnPropertyChanged(nameof(IsAddSyncPairLocalStepVisible));
        OnPropertyChanged(nameof(IsAddSyncPairCloudStepVisible));
        OnPropertyChanged(nameof(IsCreateRemoteFolderVisible));
        OnPropertyChanged(nameof(AddSyncPairWizardTitle));
        OnPropertyChanged(nameof(AddSyncPairWizardSubtitle));
        ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
        CreateRemoteFolderCommand.RaiseCanExecuteChanged();
    }

    private void SetAllPairStatuses(string status, string? currentOperation = null, bool enabledOnly = false)
    {
        foreach (SyncPairRowViewModel syncPair in SyncPairs)
        {
            if (enabledOnly && !syncPair.IsEnabled)
            {
                continue;
            }

            syncPair.Status = status;
            syncPair.CurrentOperation = currentOperation ?? string.Empty;
        }

        RaiseSyncStateProperties();
        SyncNowCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        PauseResumeCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCurrentProgressText()
    {
        if (HasActionRequired && !IsSignedIn)
        {
            CurrentProgressText = "Sign in to continue.";
            return;
        }

        if (!IsSignedIn)
        {
            CurrentProgressText = "Sign in to start sync.";
            return;
        }

        if (SyncPairs.Count == 0)
        {
            CurrentProgressText = string.Empty;
            return;
        }

        if (HasActionRequired)
        {
            CurrentProgressText = "Fix the issue below to continue syncing.";
            return;
        }

        if (!HasEnabledSyncPairs)
        {
            CurrentProgressText = "Enable a folder to start syncing.";
            return;
        }

        SyncPairRowViewModel? activePair = SyncPairs.FirstOrDefault(IsActiveProgressPair);
        if (activePair is not null)
        {
            string operation = string.IsNullOrWhiteSpace(activePair.CurrentOperation)
                ? activePair.Status
                : activePair.CurrentOperation;
            CurrentProgressText = activePair.DisplayName + ": " + operation;
            return;
        }

        if (SyncPairs.Any(static pair => string.Equals(pair.Status, "Paused", StringComparison.Ordinal)))
        {
            CurrentProgressText = "Sync is paused.";
            return;
        }

        if (HasConflicts)
        {
            CurrentProgressText = "Review conflicts below to continue syncing.";
            return;
        }

        if (SyncPairs.Any(static pair => pair.IsEnabled && pair.LastSyncedAtUtc is null))
        {
            CurrentProgressText = "Waiting for first sync.";
            return;
        }

        CurrentProgressText = "All folders are up to date.";
    }

    private void ClearTransferProgress()
    {
        HasCurrentTransfer = false;
        IsCurrentTransferIndeterminate = false;
        CurrentTransferProgressValue = 0;
        CurrentTransferTitle = string.Empty;
        CurrentTransferDetails = string.Empty;
        _transferSyncPairId = null;
        _transferDirection = SyncTransferDirection.Unknown;
        _transferRelativePath = string.Empty;
        RaiseCurrentWorkProgressProperties();
    }

    private void ClearRunProgress()
    {
        HasCurrentRunProgress = false;
        IsCurrentRunProgressIndeterminate = false;
        CurrentRunProgressValue = 0;
        CurrentRunProgressTitle = string.Empty;
        CurrentRunProgressDetails = string.Empty;
        RaiseCurrentWorkProgressProperties();
    }

    private void RaiseCurrentWorkProgressProperties()
    {
        OnPropertyChanged(nameof(HasCurrentWorkProgress));
        OnPropertyChanged(nameof(IsStatusCardVisible));
        OnPropertyChanged(nameof(CurrentWorkProgressTitle));
        OnPropertyChanged(nameof(CurrentWorkProgressDetails));
        OnPropertyChanged(nameof(CurrentWorkProgressSecondaryDetails));
        OnPropertyChanged(nameof(HasCurrentWorkProgressSecondaryDetails));
        OnPropertyChanged(nameof(CurrentWorkProgressValue));
        OnPropertyChanged(nameof(IsCurrentWorkProgressIndeterminate));
    }

    private static bool IsActiveSyncStatus(DesktopSyncPairStatusSnapshot status)
    {
        return string.Equals(status.Status, "Syncing", StringComparison.Ordinal)
            || string.Equals(status.Status, "Scanning", StringComparison.Ordinal);
    }

    private static double CalculateProgressValue(DesktopTransferProgressSnapshot progress)
    {
        if (progress.TotalBytes is > 0)
        {
            return Math.Clamp((double)progress.TransferredBytes / progress.TotalBytes.Value * 100, 0, 100);
        }

        return progress.IsCompleted ? 100 : 0;
    }

    private static double CalculateRunProgressValue(DesktopRunProgressSnapshot progress)
    {
        if (progress.FilesTotal is > 0)
        {
            return Math.Clamp((double)progress.FilesCompleted / progress.FilesTotal.Value * 100, 0, 100);
        }

        return progress.IsCompleted ? 100 : 0;
    }

    private static string CreateRunProgressTitle(DesktopRunProgressSnapshot progress, string syncPairName)
    {
        return syncPairName + ": " + GetRunStageLabel(progress.Stage);
    }

    private static string CreateRunProgressOperation(DesktopRunProgressSnapshot progress)
    {
        string label = GetRunStageLabel(progress.Stage);
        if (progress.FilesTotal.HasValue && progress.Stage == SyncRunProgressStage.ReconcilingFiles)
        {
            return label + " " + progress.FilesCompleted.ToString(CultureInfo.CurrentCulture)
                + " of " + progress.FilesTotal.Value.ToString(CultureInfo.CurrentCulture);
        }

        return label;
    }

    private static string CreateRunProgressDetails(DesktopRunProgressSnapshot progress)
    {
        if (progress.FilesTotal.HasValue)
        {
            string details = progress.FilesCompleted.ToString(CultureInfo.CurrentCulture)
                + " of "
                + progress.FilesTotal.Value.ToString(CultureInfo.CurrentCulture)
                + " files";
            if (!string.IsNullOrWhiteSpace(progress.CurrentPath))
            {
                details += " · " + GetDisplayFileName(progress.CurrentPath);
            }

            return details;
        }

        return progress.Stage switch
        {
            SyncRunProgressStage.ScanningLocal => "Looking for local changes.",
            SyncRunProgressStage.ScanningRemote => "Checking Cotton Cloud.",
            SyncRunProgressStage.ReconcilingDirectories => "Preparing folders.",
            SyncRunProgressStage.Completed => "Sync pass completed.",
            _ => "Preparing sync.",
        };
    }

    private static string GetRunStageLabel(SyncRunProgressStage stage)
    {
        return stage switch
        {
            SyncRunProgressStage.ScanningLocal => "Scanning local files",
            SyncRunProgressStage.ScanningRemote => "Scanning Cotton Cloud",
            SyncRunProgressStage.ReconcilingDirectories => "Preparing folders",
            SyncRunProgressStage.ReconcilingFiles => "Checking files",
            SyncRunProgressStage.Completed => "Finishing sync",
            _ => "Syncing",
        };
    }

    private static string CreateTransferTitle(DesktopTransferProgressSnapshot progress, string syncPairName)
    {
        string action = progress.IsCompleted
            ? progress.Direction == SyncTransferDirection.Upload ? "Uploaded" : "Downloaded"
            : progress.Direction == SyncTransferDirection.Upload ? "Uploading" : "Downloading";
        return syncPairName + ": " + action + " " + GetDisplayFileName(progress.RelativePath);
    }

    private static string CreateTransferOperation(DesktopTransferProgressSnapshot progress)
    {
        string action = progress.Direction == SyncTransferDirection.Upload ? "Uploading" : "Downloading";
        return action + " " + GetDisplayFileName(progress.RelativePath);
    }

    private static string CreateTransferDetails(DesktopTransferProgressSnapshot progress)
    {
        string size = progress.TotalBytes.HasValue
            ? FormatBytes(progress.TransferredBytes) + " / " + FormatBytes(progress.TotalBytes.Value)
            : FormatBytes(progress.TransferredBytes);
        double? bytesPerSecond = progress.SpeedBytesPerSecond;
        if (!bytesPerSecond.HasValue || bytesPerSecond.Value <= 0 || progress.IsCompleted)
        {
            return size;
        }

        string details = size + " · " + FormatBytes(bytesPerSecond.Value) + "/s";
        if (progress.EstimatedTimeRemaining.HasValue)
        {
            details += " · " + FormatDuration(progress.EstimatedTimeRemaining.Value) + " left";
        }

        return details;
    }

    private static string GetDisplayFileName(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "item";
        }

        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        string format = unitIndex == 0 || value >= 10 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unitIndex];
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
            return seconds.ToString(CultureInfo.CurrentCulture) + "s";
        }

        if (duration.TotalMinutes < 60)
        {
            return ((int)duration.TotalMinutes).ToString(CultureInfo.CurrentCulture)
                + "m "
                + duration.Seconds.ToString("00", CultureInfo.CurrentCulture)
                + "s";
        }

        return ((int)duration.TotalHours).ToString(CultureInfo.CurrentCulture)
            + "h "
            + duration.Minutes.ToString("00", CultureInfo.CurrentCulture)
            + "m";
    }

    private static bool IsActiveProgressPair(SyncPairRowViewModel syncPair)
    {
        return !string.IsNullOrWhiteSpace(syncPair.CurrentOperation)
            || string.Equals(syncPair.Status, "Scanning", StringComparison.Ordinal)
            || string.Equals(syncPair.Status, "Syncing", StringComparison.Ordinal)
            || string.Equals(syncPair.Status, "Sync requested", StringComparison.Ordinal)
            || string.Equals(syncPair.Status, "Offline", StringComparison.Ordinal)
            || string.Equals(syncPair.Status, "Error", StringComparison.Ordinal)
            || string.Equals(syncPair.Status, "Conflict", StringComparison.Ordinal);
    }

    private SyncPairRowViewModel? ResolveConflictSyncPair(ConflictRowViewModel conflict)
    {
        return conflict.SyncPairId is { } syncPairId
            ? SyncPairs.FirstOrDefault(syncPair => syncPair.Id == syncPairId)
            : SelectedSyncPair;
    }

    private static string ResolveConflictOpenPath(string localRootPath, string relativePath)
    {
        string localRoot = Path.GetFullPath(localRootPath.Trim());
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return localRoot;
        }

        string normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        string combinedPath = Path.GetFullPath(Path.Combine(localRoot, normalizedRelativePath));
        if (!IsPathInsideRoot(localRoot, combinedPath))
        {
            return localRoot;
        }

        if (Directory.Exists(combinedPath))
        {
            return combinedPath;
        }

        string? parentPath = Path.GetDirectoryName(combinedPath);
        return string.IsNullOrWhiteSpace(parentPath) || !IsPathInsideRoot(localRoot, parentPath)
            ? localRoot
            : parentPath;
    }

    private static bool IsPathInsideRoot(string localRootPath, string path)
    {
        string root = Path.GetFullPath(localRootPath);
        string candidate = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(root, candidate, comparison)
            || candidate.StartsWith(EnsureTrailingSeparator(root), comparison);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static SyncPairRowViewModel ToRow(SyncPairSettings syncPair)
    {
        return new SyncPairRowViewModel
        {
            Id = syncPair.Id,
            IsEnabled = syncPair.IsEnabled,
            DisplayName = syncPair.DisplayName,
            EditableDisplayName = syncPair.DisplayName,
            LocalPath = syncPair.LocalRootPath,
            Mode = syncPair.Mode,
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
            IsEnabled = !string.Equals(syncPair.Status, "Disabled", StringComparison.Ordinal),
            DisplayName = syncPair.DisplayName,
            EditableDisplayName = syncPair.DisplayName,
            LocalPath = syncPair.LocalPath,
            Mode = syncPair.Mode,
            RemoteRootNodeId = syncPair.RemoteRootNodeId,
            RemotePath = syncPair.RemotePath,
            Status = syncPair.Status,
            LastSyncedAtUtc = syncPair.LastSyncedAtUtc,
            ChangeCursor = syncPair.ChangeCursor,
            LastError = syncPair.LastError,
        };
    }

    private static SyncPairSettings ToSettingsForValidation(SyncPairRowViewModel syncPair)
    {
        Guid remoteRootNodeId = syncPair.RemoteRootNodeId is { } value && value != Guid.Empty
            ? value
            : Guid.NewGuid();
        return new SyncPairSettings
        {
            Id = syncPair.Id,
            DisplayName = syncPair.DisplayName,
            LocalRootPath = syncPair.LocalPath,
            RemoteRootNodeId = remoteRootNodeId,
            RemoteDisplayPath = string.IsNullOrWhiteSpace(syncPair.RemotePath) ? "/" : syncPair.RemotePath,
            IsEnabled = syncPair.IsEnabled,
            Mode = syncPair.Mode,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
    }

    private static AppThemeMode NormalizeThemeModeIndex(int index)
    {
        AppThemeMode themeMode = (AppThemeMode)index;
        return Enum.IsDefined(themeMode) ? themeMode : AppThemeMode.System;
    }

    private static string ResolveAccountDisplayName(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return "Cotton Sync";
    }

    private void RefreshDiagnosticsItems()
    {
        DiagnosticsItems.Clear();
        AddDiagnosticItem("App version", AppVersion);
        AddDiagnosticItem("Server", string.IsNullOrWhiteSpace(ServerUrl) ? "Not configured" : ServerUrl);
        AddDiagnosticItem("Account", AccountName);
        AddDiagnosticItem("Theme", ThemeModeLabel);
        AddDiagnosticItem("Data folder", string.IsNullOrWhiteSpace(DataDirectory) ? "Unknown" : DataDirectory);
        AddDiagnosticItem("Preferences database", string.IsNullOrWhiteSpace(AppDatabasePath) ? "Unknown" : AppDatabasePath);
        AddDiagnosticItem("Sync state database", string.IsNullOrWhiteSpace(SyncStateDatabasePath) ? "Unknown" : SyncStateDatabasePath);
        AddDiagnosticItem("Token store", string.IsNullOrWhiteSpace(TokenStorePath) ? "Unknown" : TokenStorePath);
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
