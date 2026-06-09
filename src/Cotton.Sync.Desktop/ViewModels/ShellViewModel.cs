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

namespace Cotton.Sync.Desktop.ViewModels
{
    /// <summary>
    /// Main desktop shell view model.
    /// </summary>
    internal class ShellViewModel : ViewModelBase, IDisposable, IAsyncDisposable
    {
        private const int MaxActivityRows = 30;
        private const int MaxConflictRows = 20;
        private const int MinimumRunProgressEstimateCompletedFiles = 5;
        private static readonly TimeSpan TransferActivityCoalescingWindow = TimeSpan.FromMilliseconds(750);
        private static readonly TimeSpan VisibleTransferProgressUpdateInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan VisibleRunProgressUpdateInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan ActiveStatusRunProgressStaleThreshold = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RunTransferMetricsWindow = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MinimumRunTransferSampleDuration = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MinimumRunProgressEstimateDuration = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan RunProgressEstimateSmoothingPeriod = TimeSpan.FromSeconds(10);

        private readonly IDesktopShellController _controller;
        private readonly DesktopFeatureFlags _featureFlags;
        private readonly ILocalFolderPicker _folderPicker;
        private readonly IDesktopNotificationService _notificationService;
        private readonly bool _notifyOnSessionRestore;
        private readonly IDesktopThemeService _themeService;
        private readonly IDesktopUiDispatcher _uiDispatcher;
        private readonly object _statusDispatchGate = new();
        private readonly object _activityDispatchGate = new();
        private readonly object _progressDispatchGate = new();
        private readonly DesktopNotificationTracker _notificationTracker = new();
        private readonly Dictionary<Guid, DesktopRunProgressSnapshot> _runProgressByPair = [];
        private readonly Dictionary<Guid, DateTime> _runProgressAppliedAtUtcByPair = [];
        private readonly Dictionary<Guid, DesktopTransferProgressSnapshot> _transferProgressByPair = [];
        private readonly Dictionary<Guid, long> _runCompletedTransferBytesByPair = [];
        private readonly Dictionary<RunTransferProgressKey, long> _runCompletedTransferBytesByKey = [];
        private readonly Dictionary<RunTransferProgressKey, long> _runTransferBytesByKey = [];
        private readonly Queue<RunFileProgressSample> _runFileProgressSamples = new();
        private readonly Queue<RunTransferProgressSample> _runTransferSamples = new();
        private readonly SyncPairSettingsValidator _syncPairSettingsValidator = new();
        private readonly Dictionary<Guid, string> _lastStatusErrorActivityMessages = [];
        private string _accountName = "Signed out";
        private string _actionRequiredMessage = string.Empty;
        private string _currentProgressText = "Sign in to start sync.";
        private string _currentRunProgressDetails = string.Empty;
        private string _currentRunProgressTitle = string.Empty;
        private string _currentTransferDetails = string.Empty;
        private string _currentTransferTitle = string.Empty;
        private long _runTransferredBytes;
        private double? _runTransferSpeedBytesPerSecond;
        private double? _currentRunProgressFilesPerSecond;
        private TimeSpan? _runTransferEstimatedTimeRemaining;
        private TimeSpan? _currentRunProgressEstimatedTimeRemaining;
        private DateTime? _lastRunTransferSpeedOccurredAtUtc;
        private DateTime? _lastRunTransferEstimateOccurredAtUtc;
        private DateTime? _lastRunProgressFileRateOccurredAtUtc;
        private DateTime? _lastRunProgressEstimateOccurredAtUtc;
        private string _dataDirectory = string.Empty;
        private string _deviceName = "Cotton Sync Desktop";
        private string _appDatabasePath = string.Empty;
        private string _syncStateDatabasePath = string.Empty;
        private string _tokenStorePath = string.Empty;
        private string _globalStatus = "Loading";
        private bool _hasCurrentRunProgress;
        private bool _hasCurrentTransfer;
        private bool _isBusy;
        private bool _isBrowserSignInPending;
        private bool _isStatusDispatchQueued;
        private bool _isCurrentRunProgressIndeterminate;
        private bool _isCurrentTransferIndeterminate;
        private bool _isSignedIn;
        private string _lastDiagnosticsBundlePath = string.Empty;
        private string _localFolderPath = string.Empty;
        private string _newRemoteFolderName = string.Empty;
        private string _password = string.Empty;
        private string _browserSignInStatus = string.Empty;
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
        private bool _isEditingSelectedSyncPairRemoteFolder;
        private bool _isLocalFolderSelectionError;
        private bool _isSelectedSyncPairEditorVisible;
        private bool _isSettingsVisible;
        private bool _isActivityVisible;
        private bool _isSyncPausePending;
        private bool _isLoadingSnapshot = true;
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
        private CancellationTokenSource? _browserSignInCancellation;
        private ConflictRowViewModel? _selectedConflict;
        private RemoteFolderRowViewModel? _selectedRemoteFolder;
        private SyncPairRowViewModel? _selectedSyncPair;
        private SyncPairRowViewModel? _pendingRemoveSyncPair;
        private string _totpCode = string.Empty;
        private SyncTransferDirection _transferDirection = SyncTransferDirection.Unknown;
        private Guid? _transferSyncPairId;
        private string _transferRelativePath = string.Empty;
        private string _username = string.Empty;
        private DesktopSyncStatusSnapshot? _pendingStatus;
        private DateTimeOffset? _lastCoalescedActivityAt;
        private Guid? _lastCoalescedActivitySyncPairId;
        private DesktopActivitySnapshot? _pendingCoalescedActivity;
        private bool _isCoalescedActivityDispatchScheduled;
        private DesktopTransferProgressSnapshot? _pendingCoalescedTransferProgress;
        private bool _isCoalescedTransferProgressDispatchScheduled;
        private DesktopRunProgressSnapshot? _pendingCoalescedRunProgress;
        private bool _isCoalescedRunProgressDispatchScheduled;
        private DateTime? _lastVisibleTransferProgressAtUtc;
        private Guid? _visibleTransferSyncPairId;
        private SyncTransferDirection _visibleTransferDirection = SyncTransferDirection.Unknown;
        private string _visibleTransferRelativePath = string.Empty;
        private DateTime? _lastVisibleRunProgressAtUtc;
        private Guid? _visibleRunProgressSyncPairId;
        private SyncRunProgressStage _visibleRunProgressStage = SyncRunProgressStage.Unknown;

        internal ShellViewModel(
            IDesktopShellController controller,
            ILocalFolderPicker folderPicker,
            IDesktopNotificationService notificationService,
            IDesktopThemeService themeService,
            IDesktopUiDispatcher? uiDispatcher = null,
            DesktopFeatureFlags? featureFlags = null,
            bool notifyOnSessionRestore = false)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _featureFlags = featureFlags ?? DesktopFeatureFlags.Default;
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _notifyOnSessionRestore = notifyOnSessionRestore;
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _uiDispatcher = uiDispatcher ?? new AvaloniaDesktopUiDispatcher();
            Activities.CollectionChanged += OnActivitiesChanged;
            Conflicts.CollectionChanged += OnConflictsChanged;
            SyncPairs.CollectionChanged += OnSyncPairsChanged;
            RemoteFolders.CollectionChanged += OnRemoteFoldersChanged;
            SelfTestItems.CollectionChanged += OnSelfTestItemsChanged;
            Notifications.CollectionChanged += OnNotificationsChanged;
            _controller.ActivityReported += OnActivityReported;
            _controller.SessionRevoked += OnSessionRevoked;
            _controller.TransferProgressChanged += OnTransferProgressChanged;
            _controller.RunProgressChanged += OnRunProgressChanged;
            _controller.StatusChanged += OnStatusChanged;
            SignInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn, HandleCommandError);
            SignInWithBrowserCommand = new AsyncRelayCommand(
                SignInWithBrowserAsync,
                CanSignInWithBrowser,
                HandleCommandError);
            CancelBrowserSignInCommand = new AsyncRelayCommand(
                CancelBrowserSignInAsync,
                CanCancelBrowserSignIn,
                HandleCommandError);
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
            ShowSettingsCommand = new AsyncRelayCommand(ShowSettingsAsync, () => IsSignedIn, HandleCommandError);
            CloseSettingsCommand = new AsyncRelayCommand(CloseSettingsAsync, () => IsSettingsVisible, HandleCommandError);
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
            ChangeSelectedSyncPairLocalFolderCommand = new AsyncRelayCommand(
                ChangeSelectedSyncPairLocalFolderAsync,
                () => IsSignedIn && SelectedSyncPair is not null && !IsBusy,
                HandleCommandError);
            ChangeSelectedSyncPairRemoteFolderCommand = new AsyncRelayCommand(
                ShowSelectedSyncPairRemoteFolderAsync,
                () => IsSignedIn && SelectedSyncPair is not null && !IsBusy && CanUseAddSyncPairFlow,
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
            ToggleActivityCommand = new AsyncRelayCommand(ToggleActivityAsync, () => IsSignedIn, HandleCommandError);
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
            UseRemoteFolderCommand = new AsyncRelayCommand(UseRemoteFolderAsync, CanUseRemoteFolder, HandleCommandError);
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

        public AsyncRelayCommand CancelBrowserSignInCommand { get; }

        public AsyncRelayCommand CancelCreateRemoteFolderCommand { get; }

        public AsyncRelayCommand CancelRemoveSyncPairCommand { get; }

        public AsyncRelayCommand CancelSelectedSyncPairEditorCommand { get; }

        public AsyncRelayCommand ChangeServerCommand { get; }

        public AsyncRelayCommand ChangeSelectedSyncPairLocalFolderCommand { get; }

        public AsyncRelayCommand ChangeSelectedSyncPairRemoteFolderCommand { get; }

        public AsyncRelayCommand CloseSettingsCommand { get; }

        public AsyncRelayCommand ConfirmRemoveSelectedSyncPairCommand { get; }

        public AsyncRelayCommand CreateRemoteFolderCommand { get; }

        public AsyncRelayCommand OpenDiagnosticsBundleFolderCommand { get; }

        public AsyncRelayCommand OpenDataFolderCommand { get; }

        public AsyncRelayCommand OpenFolderCommand { get; }

        public AsyncRelayCommand OpenConflictCommand { get; }

        public AsyncRelayCommand OpenTrayFolderCommand { get; }

        public AsyncRelayCommand OpenWebCommand { get; }

        public AsyncRelayCommand ToggleActivityCommand { get; }

        public AsyncRelayCommand OpenRemoteFolderCommand { get; }

        public AsyncRelayCommand RemoveSelectedSyncPairCommand { get; }

        public AsyncRelayCommand SaveSelectedSyncPairNameCommand { get; }

        public AsyncRelayCommand ToggleSelectedSyncPairEnabledCommand { get; }

        public AsyncRelayCommand UseRemoteFolderCommand { get; }

        public AsyncRelayCommand PauseCommand { get; }

        public AsyncRelayCommand PauseResumeCommand { get; }

        public AsyncRelayCommand ResumeCommand { get; }

        public AsyncRelayCommand RemoteFolderUpCommand { get; }

        public AsyncRelayCommand SignInCommand { get; }

        public AsyncRelayCommand SignInWithBrowserCommand { get; }

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

        public string DeviceName
        {
            get => _deviceName;
            private set => SetProperty(ref _deviceName, value);
        }

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
                    OnPropertyChanged(nameof(HeaderStatusText));
                    OnPropertyChanged(nameof(IsStatusCardVisible));
                    OnPropertyChanged(nameof(ActionRequiredOpacity));
                    OnPropertyChanged(nameof(CanRetryActionRequired));
                    OnPropertyChanged(nameof(StatusCardTitle));
                    OnPropertyChanged(nameof(StatusCardDetailText));
                    OnPropertyChanged(nameof(HasStatusCardDetail));
                    OnPropertyChanged(nameof(HasDashboardNotifications));
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

        public string HeaderStatusText
        {
            get
            {
                if (HasConflicts)
                {
                    return "Conflicts need review";
                }

                if (HasActionRequired || HasPairStatusAttention)
                {
                    return "Action required";
                }

                if (IsSyncPaused || IsSyncPausePending)
                {
                    return GlobalStatus;
                }

                return HasCurrentWorkProgress ? "Syncing" : GlobalStatus;
            }
        }

        public string HeaderTitleText => IsSignedIn ? ResolveAccountDisplayName(AccountName, null) : "Cotton Sync";

        public string StatusCardTitle
        {
            get
            {
                if (HasActionRequired || HasPairStatusAttention)
                {
                    return "Sync needs attention";
                }

                return CurrentProgressText;
            }
        }

        public string StatusCardDetailText => HasActionRequired || HasPairStatusAttention ? CurrentProgressText : string.Empty;

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

        public string CurrentWorkProgressTitle
        {
            get => IsRunProgressPrimary
                ? CurrentRunProgressTitle
                : HasCurrentTransfer ? CurrentTransferTitle : CurrentRunProgressTitle;
        }

        public string CurrentWorkProgressHeaderDetails => IsRunProgressPrimary
            ? CreateHeaderDetails(CurrentWorkProgressHeaderSizeDetails, CurrentWorkProgressHeaderRateDetails)
            : string.Empty;

        public bool HasCurrentWorkProgressHeaderDetails => !string.IsNullOrWhiteSpace(CurrentWorkProgressHeaderDetails);

        public string CurrentWorkProgressHeaderSizeDetails => IsRunProgressPrimary
            ? CreateRunTransferSizeDetails()
            : string.Empty;

        public bool HasCurrentWorkProgressHeaderSizeDetails =>
            !string.IsNullOrWhiteSpace(CurrentWorkProgressHeaderSizeDetails)
            || !string.IsNullOrWhiteSpace(CurrentWorkProgressHeaderRateDetails);

        public string CurrentWorkProgressHeaderRateDetails => IsRunProgressPrimary
            ? CreateRunTransferRateDetails()
            : string.Empty;

        public bool HasCurrentWorkProgressHeaderRateDetails =>
            !string.IsNullOrWhiteSpace(CurrentWorkProgressHeaderRateDetails);

        public string CurrentWorkProgressDetails => IsRunProgressPrimary
            ? CurrentRunProgressDetails
            : HasCurrentTransfer ? CurrentTransferDetails : CurrentRunProgressDetails;

        public string CurrentWorkProgressSecondaryDetails
        {
            get
            {
                if (IsRunProgressPrimary)
                {
                    return string.Empty;
                }

                return HasCurrentTransfer && HasCurrentRunProgress
                    ? CurrentRunProgressDetails
                    : string.Empty;
            }
        }

        public bool HasCurrentWorkProgressSecondaryDetails => !string.IsNullOrWhiteSpace(CurrentWorkProgressSecondaryDetails);

        public double CurrentWorkProgressValue => IsRunProgressPrimary
            ? CurrentRunProgressValue
            : TryCalculateAggregateTransferProgressValue(out double transferProgressValue)
                ? transferProgressValue
                : HasCurrentTransfer ? CurrentTransferProgressValue : CurrentRunProgressValue;

        public bool IsCurrentWorkProgressIndeterminate => IsRunProgressPrimary
            ? IsCurrentRunProgressIndeterminate
            : HasActiveTransferProgress
                ? !TryCalculateAggregateTransferProgressValue(out _)
                : HasCurrentTransfer ? IsCurrentTransferIndeterminate : IsCurrentRunProgressIndeterminate;

        private bool IsRunProgressPrimary => HasCurrentRunProgress;

        private bool HasActiveTransferProgress => _transferProgressByPair.Count > 0;

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

        public bool IsBrowserSignInPending
        {
            get => _isBrowserSignInPending;
            private set
            {
                if (SetProperty(ref _isBrowserSignInPending, value))
                {
                    OnPropertyChanged(nameof(BrowserSignInButtonText));
                    OnPropertyChanged(nameof(IsPasswordSignInVisible));
                    CancelBrowserSignInCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string BrowserSignInStatus
        {
            get => _browserSignInStatus;
            private set
            {
                if (SetProperty(ref _browserSignInStatus, value))
                {
                    OnPropertyChanged(nameof(HasBrowserSignInStatus));
                }
            }
        }

        public bool HasBrowserSignInStatus => !string.IsNullOrWhiteSpace(BrowserSignInStatus);

        public string BrowserSignInButtonText => IsBrowserSignInPending
            ? "Waiting for approval"
            : "Open browser";

        public bool IsPasswordSignInVisible => !IsBrowserSignInPending;

        public bool IsSignedIn
        {
            get => _isSignedIn;
            private set
            {
                if (SetProperty(ref _isSignedIn, value))
                {
                    OnPropertyChanged(nameof(IsDashboardVisible));
                    OnPropertyChanged(nameof(IsDashboardHeaderVisible));
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

        public bool IsActivityVisible
        {
            get => _isActivityVisible;
            private set
            {
                if (SetProperty(ref _isActivityVisible, value))
                {
                    OnPropertyChanged(nameof(IsActivityHidden));
                    OnPropertyChanged(nameof(ActivityToggleToolTip));
                }
            }
        }

        public bool IsActivityHidden => !IsActivityVisible;

        public string ActivityToggleToolTip => IsActivityVisible ? "Hide activity" : "Show activity";

        public bool HasConflicts => Conflicts.Count > 0;

        public string ConflictCountLabel => Conflicts.Count == 1 ? "1 conflict" : Conflicts.Count + " conflicts";

        public bool HasActionRequired => !string.IsNullOrWhiteSpace(ActionRequiredMessage);

        public bool HasStatusAttention => HasActionRequired || HasConflicts || HasPairStatusAttention;

        private bool HasPairStatusAttention => SyncPairs.Any(static pair => pair.IsStatusAttention);

        public bool IsStatusCardVisible =>
            HasSyncPairs
            && !HasActionRequired
            && !HasConflicts
            && !HasCurrentWorkProgress
            && !HasHealthySyncedIdleState;

        public bool IsDashboardChromeVisible => !IsAddSyncPairWizardVisible && !IsSettingsVisible;

        public bool IsDashboardHeaderVisible => IsDashboardVisible && !IsAddSyncPairWizardVisible && !IsSettingsVisible;

        public double ActionRequiredOpacity => HasActionRequired ? 1 : 0;

        public bool CanRetryActionRequired => HasActionRequired && IsSignedIn;

        public bool HasNoRemoteFolders => RemoteFolders.Count == 0;

        public bool HasRemoteFolders => RemoteFolders.Count > 0;

        public bool HasNoSelfTestItems => SelfTestItems.Count == 0;

        public bool HasSelfTestItems => SelfTestItems.Count > 0;

        public bool HasNotifications => Notifications.Count > 0;

        public bool HasDashboardNotifications =>
            Notifications.Any(static notification => notification.IsDashboardVisible)
            && !HasStatusAttention
            && !IsStatusCardVisible
            && !HasCurrentWorkProgress;

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

        public bool CanSyncNow => IsSignedIn && !IsBusy && HasEnabledSyncPairs && !IsSyncPaused && !IsSyncPausePending;

        public bool CanPauseSync => IsSignedIn && HasEnabledSyncPairs && !IsSyncPaused && !IsSyncPausePending;

        public bool CanResumeSync => IsSignedIn && IsSyncPaused;

        public bool CanTogglePauseResumeSync => CanPauseSync || CanResumeSync;

        public string PauseResumeSyncLabel => IsSyncPausePending ? "Pausing sync" : IsSyncPaused ? "Resume sync" : "Pause sync";

        public string PauseResumeTrayLabel => IsSyncPausePending ? "Pausing" : IsSyncPaused ? "Resume" : "Pause";

        public bool CanOpenTrayFolder => IsSignedIn && !IsBusy && SyncPairs.Count == 1;

        public string TrayOpenFolderLabel => "Open local folder";

        public bool IsSyncPaused => HasEnabledSyncPairs
            && SyncPairs
                .Where(static syncPair => syncPair.IsEnabled)
                .All(static syncPair => string.Equals(syncPair.Status, "Paused", StringComparison.Ordinal));

        public bool IsSyncPausePending
        {
            get => _isSyncPausePending;
            private set
            {
                if (SetProperty(ref _isSyncPausePending, value))
                {
                    RaiseSyncStateProperties();
                }
            }
        }

        private bool HasEnabledSyncPairs => SyncPairs.Any(static syncPair => syncPair.IsEnabled);

        private bool HasHealthySyncedIdleState
        {
            get
            {
                bool hasEnabledPair = false;
                foreach (SyncPairRowViewModel syncPair in SyncPairs)
                {
                    if (!syncPair.IsEnabled)
                    {
                        continue;
                    }

                    hasEnabledPair = true;
                    if (!syncPair.LastSyncedAtUtc.HasValue
                        || !string.Equals(syncPair.Status, "Idle", StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                return hasEnabledPair;
            }
        }

        public bool IsStartupLoadingVisible => _isLoadingSnapshot;

        public bool IsDashboardVisible => IsSignedIn && !IsStartupLoadingVisible;

        public bool IsSetupVisible => !IsSignedIn && !IsStartupLoadingVisible;

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
                    return "Autostart is not available for this launch. Publish or install Cotton Sync to enable startup registration.";
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
                    OnPropertyChanged(nameof(IsDashboardHeaderVisible));
                }
            }
        }

        public bool HasLocalFolderSelection => !string.IsNullOrWhiteSpace(LocalFolderPath);

        public bool IsAddSyncPairLocalStepVisible =>
            IsAddSyncPairWizardVisible && !IsEditingSelectedSyncPairRemoteFolder && !HasLocalFolderSelection;

        public bool IsAddSyncPairCloudStepVisible =>
            IsAddSyncPairWizardVisible && (HasLocalFolderSelection || IsEditingSelectedSyncPairRemoteFolder);

        public bool IsAddSyncPairLocalSummaryVisible =>
            IsAddSyncPairCloudStepVisible && !IsEditingSelectedSyncPairRemoteFolder;

        public bool IsEditingSelectedSyncPairRemoteFolder
        {
            get => _isEditingSelectedSyncPairRemoteFolder;
            private set
            {
                if (SetProperty(ref _isEditingSelectedSyncPairRemoteFolder, value))
                {
                    RaiseWizardStateProperties();
                    RaiseAddSyncPairFlowCommandStates();
                }
            }
        }

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
                    OnPropertyChanged(nameof(IsDashboardHeaderVisible));
                    CloseSettingsCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int SelectedSettingsTabIndex
        {
            get => _selectedSettingsTabIndex;
            set => SetProperty(ref _selectedSettingsTabIndex, value);
        }

        public string AddSyncPairWizardTitle => IsEditingSelectedSyncPairRemoteFolder
            ? "Change cloud folder"
            : HasLocalFolderSelection ? "Choose cloud folder" : "Choose local folder";

        public string AddSyncPairWizardSubtitle => IsEditingSelectedSyncPairRemoteFolder
            ? $"Pick the Cotton Cloud folder for {SelectedSyncPair?.DisplayName ?? "this sync folder"}."
            : HasLocalFolderSelection
                ? "Pick where this computer folder should sync in Cotton Cloud."
                : "Start with the folder on this computer.";

        public string RemoteFolderWizardPrimaryActionText => IsEditingSelectedSyncPairRemoteFolder
            ? "Update cloud folder"
            : "Use this folder";

        public string RemoteFolderWizardPrimaryActionToolTip => IsEditingSelectedSyncPairRemoteFolder
            ? "Change the cloud folder for this sync folder"
            : "Start syncing with the current cloud folder";

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
                    SignInWithBrowserCommand.RaiseCanExecuteChanged();
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
                    UseRemoteFolderCommand.RaiseCanExecuteChanged();
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
                    UseRemoteFolderCommand.RaiseCanExecuteChanged();
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
                    if (!_isLoadingSnapshot && !IsSignedIn && HasActionRequired)
                    {
                        ActionRequiredMessage = string.Empty;
                    }

                    ScheduleServerProbe(value);
                    SignInCommand.RaiseCanExecuteChanged();
                    SignInWithBrowserCommand.RaiseCanExecuteChanged();
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
                    OnPropertyChanged(nameof(AddSyncPairWizardSubtitle));
                    if (_pendingRemoveSyncPair is not null && !ReferenceEquals(_pendingRemoveSyncPair, value))
                    {
                        ClearRemoveSyncPairConfirmation();
                    }

                    OpenFolderCommand.RaiseCanExecuteChanged();
                    ChangeSelectedSyncPairLocalFolderCommand.RaiseCanExecuteChanged();
                    ChangeSelectedSyncPairRemoteFolderCommand.RaiseCanExecuteChanged();
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
            _controller.SessionRevoked -= OnSessionRevoked;
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
            _serverProbeCancellation = null;
            _browserSignInCancellation?.Cancel();
            _browserSignInCancellation?.Dispose();
            _browserSignInCancellation = null;
        }

        public async Task InitializeAsync()
        {
            IsBusy = true;
            SetSnapshotLoading(true);
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
                DeviceName = string.IsNullOrWhiteSpace(snapshot.DeviceName)
                    ? "Cotton Sync Desktop"
                    : snapshot.DeviceName.Trim();
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
                    if (_notifyOnSessionRestore)
                    {
                        ShowNativeNotification("Session restored", AccountName);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(snapshot.StartupErrorMessage))
                {
                    ActionRequiredMessage = snapshot.StartupErrorMessage;
                    AddActivity("Error", string.Empty, snapshot.StartupErrorMessage);
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
                SetSnapshotLoading(false);
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

            SyncPairRowViewModel? secondSyncPair = SyncPairs.Skip(1).FirstOrDefault();
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
                startedAtUtc.AddSeconds(6)));
            if (secondSyncPair is not null)
            {
                secondSyncPair.Status = "Syncing";
                ApplyRunProgress(new DesktopRunProgressSnapshot(
                    secondSyncPair.Id,
                    SyncRunProgressStage.ReconcilingFiles,
                    FilesCompleted: 2,
                    FilesTotal: 9,
                    CurrentPath: "Blink/2024/07.7z",
                    startedAtUtc,
                    IsCompleted: false,
                    startedAtUtc.AddSeconds(6)));
            }

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
            if (secondSyncPair is not null)
            {
                ApplyTransferProgress(new DesktopTransferProgressSnapshot(
                    secondSyncPair.Id,
                    SyncTransferDirection.Download,
                    "Blink/2024/07.7z",
                    TransferredBytes: 1_048_576,
                    TotalBytes: 3_145_728,
                    IsCompleted: false,
                    startedAtUtc.AddSeconds(2),
                    SpeedBytesPerSecond: 1_048_576,
                    EstimatedTimeRemaining: TimeSpan.FromSeconds(2)));
            }

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
                IsEditingSelectedSyncPairRemoteFolder = false;
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
            IsEditingSelectedSyncPairRemoteFolder = false;
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

        private Task ToggleActivityAsync()
        {
            IsActivityVisible = !IsActivityVisible;
            return Task.CompletedTask;
        }

        private async Task ToggleSelectedSyncPairEnabledAsync()
        {
            SyncPairRowViewModel? selected = SelectedSyncPair;
            if (selected is null)
            {
                return;
            }

            bool enabled = !selected.IsEnabled;
            bool wasSyncPaused = IsSyncPaused;
            IsBusy = true;
            try
            {
                await _controller.SetSyncPairEnabledAsync(selected.Id, enabled).ConfigureAwait(true);
                selected.IsEnabled = enabled;
                OnPropertyChanged(nameof(SelectedSyncPairToggleEnabledLabel));
                if (enabled)
                {
                    selected.Status = wasSyncPaused ? "Paused" : "Idle";
                }
                else
                {
                    selected.Status = "Disabled";
                }

                selected.CurrentOperation = string.Empty;
                if (wasSyncPaused && HasEnabledSyncPairs)
                {
                    GlobalStatus = "Paused";
                }
                else
                {
                    GlobalStatus = enabled ? "Ready" : "Folder disabled";
                }

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

        private async Task ChangeSelectedSyncPairLocalFolderAsync()
        {
            SyncPairRowViewModel? selected = SelectedSyncPair;
            if (selected is null)
            {
                return;
            }

            string? selectedPath = await _folderPicker.PickFolderAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            string normalizedSelectedPath = selectedPath.Trim();
            string? overlapMessage = GetLocalFolderOverlapMessage(normalizedSelectedPath, selected.Id);
            if (overlapMessage is not null)
            {
                GlobalStatus = "Action required";
                ActionRequiredMessage = overlapMessage;
                AddActivity("Warning", normalizedSelectedPath, ActionRequiredMessage);
                RefreshCurrentProgressText();
                return;
            }

            IsBusy = true;
            try
            {
                await _controller.SetSyncPairLocalFolderAsync(selected.Id, normalizedSelectedPath).ConfigureAwait(true);
                selected.LocalPath = normalizedSelectedPath;
                GlobalStatus = "Folder updated";
                ActionRequiredMessage = string.Empty;
                AddActivity("Pair", normalizedSelectedPath, "Local folder changed");
                RefreshCurrentProgressText();
                RefreshDiagnosticsItems();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShowSelectedSyncPairRemoteFolderAsync()
        {
            SyncPairRowViewModel? selected = SelectedSyncPair;
            if (selected is null)
            {
                return;
            }

            ClearRemoveSyncPairConfirmation();
            LocalFolderPath = selected.LocalPath;
            RemoteFolderPath = string.Empty;
            IsEditingSelectedSyncPairRemoteFolder = true;
            IsAddSyncPairWizardVisible = true;
            NewRemoteFolderName = string.Empty;
            IsCreateRemoteFolderVisible = false;

            string remotePath = string.IsNullOrWhiteSpace(selected.RemotePath) ? "/" : selected.RemotePath;
            await LoadRemoteFoldersAsync(remotePath).ConfigureAwait(true);
        }

        private Task UseRemoteFolderAsync()
        {
            return IsEditingSelectedSyncPairRemoteFolder
                ? ChangeSelectedSyncPairRemoteFolderAsync()
                : AddSyncPairAsync();
        }

        private async Task ChangeSelectedSyncPairRemoteFolderAsync()
        {
            SyncPairRowViewModel? selected = SelectedSyncPair;
            if (selected is null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                SyncPairSettings syncPair = await _controller
                    .SetSyncPairRemoteFolderAsync(selected.Id, RemoteFolderPath)
                    .ConfigureAwait(true);
                selected.RemoteRootNodeId = syncPair.RemoteRootNodeId;
                selected.RemotePath = syncPair.RemoteDisplayPath;
                LocalFolderPath = string.Empty;
                IsEditingSelectedSyncPairRemoteFolder = false;
                IsAddSyncPairWizardVisible = false;
                ActionRequiredMessage = string.Empty;
                ResetRemoteFolderSelection();
                GlobalStatus = "Cloud folder updated";
                AddActivity("Pair", selected.LocalPath, "Cloud folder changed to " + selected.RemotePath);
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

            if (ReferenceEquals(SelectedSyncPair, target) && IsSelectedSyncPairEditorVisible)
            {
                ClearRemoveSyncPairConfirmation();
                IsSelectedSyncPairEditorVisible = false;
                return Task.CompletedTask;
            }

            SelectedSyncPair = target;
            ClearRemoveSyncPairConfirmation();
            IsSelectedSyncPairEditorVisible = true;
            IsActivityVisible = false;
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
            IsSyncPausePending = true;
            GlobalStatus = "Pausing";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Pausing", enabledOnly: true);
            RefreshCurrentProgressText();
            AddActivity("Sync", string.Empty, "Synchronization pause requested");
            try
            {
                await _controller.PauseAllAsync().ConfigureAwait(true);
                GlobalStatus = "Paused";
                SetAllPairStatuses("Paused", enabledOnly: true);
                RefreshCurrentProgressText();
                AddActivity("Sync", string.Empty, "Synchronization paused");
            }
            finally
            {
                IsSyncPausePending = false;
            }
        }

        private Task PauseResumeAsync()
        {
            return IsSyncPaused ? ResumeAsync() : PauseAsync();
        }

        private async Task ResumeAsync()
        {
            await _controller.ResumeAllAsync().ConfigureAwait(true);
            GlobalStatus = "Ready";
            ActionRequiredMessage = string.Empty;
            SetAllPairStatuses("Idle", enabledOnly: true);
            RefreshCurrentProgressText();
            AddActivity("Sync", string.Empty, "Synchronization resumed");
        }

        private async Task SignInAsync()
        {
            IsBusy = true;
            try
            {
                AuthSession session = await _controller.SignInAsync(
                    new DesktopSignInRequest(ServerUrl, Username, Password, TotpCode)).ConfigureAwait(true);
                ApplySignedInSession(session, "Signed in");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SignInWithBrowserAsync()
        {
            using var cancellation = new CancellationTokenSource();
            _browserSignInCancellation = cancellation;
            IsBrowserSignInPending = true;
            BrowserSignInStatus = "Approve this sign-in in your browser.";
            IsBusy = true;
            GlobalStatus = "Waiting for browser sign-in";
            ActionRequiredMessage = string.Empty;
            try
            {
                AuthSession session = await _controller.SignInWithBrowserAsync(ServerUrl, cancellation.Token)
                    .ConfigureAwait(true);
                ApplySignedInSession(session, "Signed in with browser");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                GlobalStatus = "Sign-in cancelled";
                BrowserSignInStatus = string.Empty;
                AddActivity("Account", string.Empty, "Browser sign-in cancelled");
            }
            finally
            {
                if (ReferenceEquals(_browserSignInCancellation, cancellation))
                {
                    _browserSignInCancellation = null;
                }

                IsBrowserSignInPending = false;
                BrowserSignInStatus = string.Empty;
                IsBusy = false;
            }
        }

        private Task CancelBrowserSignInAsync()
        {
            _browserSignInCancellation?.Cancel();
            BrowserSignInStatus = "Cancelling browser sign-in.";
            GlobalStatus = "Cancelling sign-in";
            return Task.CompletedTask;
        }

        private void ApplySignedInSession(AuthSession session, string activityDetails)
        {
            IsSignedIn = true;
            AccountName = ResolveAccountDisplayName(session.Email, session.Username);
            Username = AccountName;
            Password = string.Empty;
            TotpCode = string.Empty;
            GlobalStatus = "Connected";
            ActionRequiredMessage = string.Empty;
            AddActivity("Account", AccountName, activityDetails);
            ShowNativeNotification("Signed in", AccountName);
            RefreshDiagnosticsItems();
        }

        private async Task SignOutAsync()
        {
            IsBusy = true;
            try
            {
                await _controller.SignOutAsync().ConfigureAwait(true);
                ApplySignedOutState("Signed out");
                AddActivity("Account", string.Empty, "Signed out");
                ShowNativeNotification("Signed out", "Cotton Sync is signed out.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplySignedOutState(string globalStatus)
        {
            IsSignedIn = false;
            AccountName = "Signed out";
            GlobalStatus = globalStatus;
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
            RefreshDiagnosticsItems();
        }

        private async Task SyncNowAsync()
        {
            IsBusy = true;
            try
            {
                await _controller.SyncAllAsync().ConfigureAwait(true);
                GlobalStatus = "Checked for changes";
                ActionRequiredMessage = string.Empty;
                RefreshCurrentProgressText();
                AddActivity("Sync", string.Empty, "Manual sync completed");
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
                string selfTestActionRequiredMessage = DesktopActionRequiredMessageResolver.FromSelfTest(result);
                string syncPairActionRequiredMessage = ResolveCurrentSyncPairActionRequiredMessage();
                string actionRequiredMessage = string.IsNullOrWhiteSpace(selfTestActionRequiredMessage)
                    ? syncPairActionRequiredMessage
                    : selfTestActionRequiredMessage;
                SetDesktopSyncChangesApiUnavailable(HasMissingDesktopSyncChangesApiFailure(result));
                GlobalStatus = string.IsNullOrWhiteSpace(actionRequiredMessage) ? "Self-test passed" : "Action required";
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

        private string ResolveCurrentSyncPairActionRequiredMessage()
        {
            if (SyncPairs.Count == 0)
            {
                return string.Empty;
            }

            DesktopSyncPairStatusSnapshot[] pairStatuses = SyncPairs
                .Select(static pair => new DesktopSyncPairStatusSnapshot(
                    pair.Id,
                    pair.Status,
                    pair.LastError,
                    pair.CurrentOperation,
                    pair.LastSyncedAtUtc))
                .ToArray();

            return DesktopActionRequiredMessageResolver.FromStatus(new DesktopSyncStatusSnapshot(pairStatuses));
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
            IsEditingSelectedSyncPairRemoteFolder = false;
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
                && !IsEditingSelectedSyncPairRemoteFolder
                && !string.IsNullOrWhiteSpace(LocalFolderPath)
                && !string.IsNullOrWhiteSpace(RemoteFolderPath);
        }

        private bool CanBrowseLocalFolder()
        {
            return !IsBusy && CanUseAddSyncPairFlow && !IsEditingSelectedSyncPairRemoteFolder;
        }

        private bool CanUseRemoteFolder()
        {
            return IsEditingSelectedSyncPairRemoteFolder
                ? CanChangeSelectedSyncPairRemoteFolder()
                : CanAddSyncPair();
        }

        private bool CanChangeSelectedSyncPairRemoteFolder()
        {
            return !IsBusy
                && CanUseAddSyncPairFlow
                && IsSignedIn
                && SelectedSyncPair is not null
                && IsAddSyncPairCloudStepVisible
                && !string.IsNullOrWhiteSpace(RemoteFolderPath);
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

        private string? GetLocalFolderOverlapMessage(string localPath, Guid? existingSyncPairId = null)
        {
            if (SyncPairs.Count == 0 && !existingSyncPairId.HasValue)
            {
                return null;
            }

            Guid candidateId = existingSyncPairId ?? Guid.NewGuid();
            List<SyncPairSettings> syncPairs = SyncPairs
                .Where(pair => pair.Id != candidateId)
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

        private bool CanSignInWithBrowser()
        {
            return !IsBusy
                && !string.IsNullOrWhiteSpace(ServerUrl)
                && IsServerVerified;
        }

        private bool CanCancelBrowserSignIn()
        {
            return IsBrowserSignInPending && _browserSignInCancellation is not null;
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

        private async Task ProbeServerAfterDelayAsync(
            string serverUrl,
            CancellationTokenSource probeCancellation)
        {
            CancellationToken cancellationToken = probeCancellation.Token;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken).ConfigureAwait(false);
                DesktopServerProbeResult result = await _controller.ProbeServerAsync(serverUrl, cancellationToken)
                    .ConfigureAwait(false);
                await _uiDispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsCurrentServerProbe(serverUrl, probeCancellation))
                        {
                            return;
                        }

                        ApplyServerProbeResult(result);
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Failed to probe Cotton server {0}: {1}", serverUrl, exception);
                await _uiDispatcher.InvokeAsync(
                    () =>
                    {
                        if (!IsCurrentServerProbe(serverUrl, probeCancellation))
                        {
                            return;
                        }

                        ApplyServerProbeFailure();
                    },
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
            _ = ProbeServerAfterDelayAsync(normalized, _serverProbeCancellation);
        }

        private bool IsCurrentServerProbe(string serverUrl, CancellationTokenSource probeCancellation)
        {
            return ReferenceEquals(_serverProbeCancellation, probeCancellation)
                && !probeCancellation.IsCancellationRequested
                && string.Equals(ServerUrl.Trim(), serverUrl, StringComparison.Ordinal);
        }

        private void OnSyncPairsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasNoSyncPairs));
            OnPropertyChanged(nameof(HasSyncPairs));
            OnPropertyChanged(nameof(IsStatusCardVisible));
            OnPropertyChanged(nameof(HasDashboardNotifications));
            RaiseSyncStateProperties();
            OpenFolderCommand.RaiseCanExecuteChanged();
            RaiseTrayOpenFolderState();
            ChangeSelectedSyncPairLocalFolderCommand.RaiseCanExecuteChanged();
            ChangeSelectedSyncPairRemoteFolderCommand.RaiseCanExecuteChanged();
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
            OnPropertyChanged(nameof(HasDashboardNotifications));
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
            OnPropertyChanged(nameof(HasDashboardNotifications));
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
            if (_uiDispatcher.CheckAccess())
            {
                ApplyStatus(status);
                return;
            }

            PostCoalescedStatus(status);
        }

        private void PostCoalescedStatus(DesktopSyncStatusSnapshot status)
        {
            lock (_statusDispatchGate)
            {
                _pendingStatus = status;
                if (_isStatusDispatchQueued)
                {
                    return;
                }

                _isStatusDispatchQueued = true;
            }

            _uiDispatcher.Post(ApplyPendingStatus);
        }

        private void ApplyPendingStatus()
        {
            DesktopSyncStatusSnapshot? status;
            lock (_statusDispatchGate)
            {
                status = _pendingStatus;
                _pendingStatus = null;
                _isStatusDispatchQueued = false;
            }

            if (status is not null)
            {
                ApplyStatus(status);
            }
        }

        private void OnActivityReported(object? sender, DesktopActivitySnapshot activity)
        {
            if (_uiDispatcher.CheckAccess())
            {
                ApplyActivity(activity);
                return;
            }

            if (TryPostCoalescedActivity(activity))
            {
                return;
            }

            _uiDispatcher.Post(() => ApplyActivity(activity));
        }

        private bool TryPostCoalescedActivity(DesktopActivitySnapshot activity)
        {
            if (!IsHighVolumeTransferActivity(activity.Kind))
            {
                return false;
            }

            lock (_activityDispatchGate)
            {
                if (_pendingCoalescedActivity is not null
                    && CanReplacePendingActivity(_pendingCoalescedActivity, activity))
                {
                    _pendingCoalescedActivity = activity;
                    return true;
                }

                if (_isCoalescedActivityDispatchScheduled)
                {
                    return false;
                }

                _pendingCoalescedActivity = activity;
                _isCoalescedActivityDispatchScheduled = true;
            }

            _uiDispatcher.Post(ApplyPendingCoalescedActivity);
            return true;
        }

        private void ApplyPendingCoalescedActivity()
        {
            DesktopActivitySnapshot? activity;
            lock (_activityDispatchGate)
            {
                activity = _pendingCoalescedActivity;
                _pendingCoalescedActivity = null;
                _isCoalescedActivityDispatchScheduled = false;
            }

            if (activity is not null)
            {
                ApplyActivity(activity);
            }
        }

        private static bool CanReplacePendingActivity(
            DesktopActivitySnapshot pending,
            DesktopActivitySnapshot next)
        {
            if (!IsHighVolumeTransferActivity(next.Kind)
                || !string.Equals(pending.Kind, next.Kind, StringComparison.Ordinal)
                || !Equals(pending.SyncPairId, next.SyncPairId)
                || next.OccurredAtUtc < pending.OccurredAtUtc)
            {
                return false;
            }

            return next.OccurredAtUtc - pending.OccurredAtUtc <= TransferActivityCoalescingWindow;
        }

        private void OnSessionRevoked(object? sender, DesktopSessionRevocationSnapshot sessionRevocation)
        {
            if (_uiDispatcher.CheckAccess())
            {
                ApplySessionRevocation(sessionRevocation);
                return;
            }

            _uiDispatcher.Post(() => ApplySessionRevocation(sessionRevocation));
        }

        private void OnTransferProgressChanged(object? sender, DesktopTransferProgressSnapshot progress)
        {
            if (_uiDispatcher.CheckAccess())
            {
                if (ShouldQueueVisibleTransferProgress(progress))
                {
                    ApplyTransferProgress(progress);
                }

                return;
            }

            if (TryPostCoalescedTransferProgress(progress))
            {
                return;
            }

            if (ShouldQueueVisibleTransferProgress(progress))
            {
                _uiDispatcher.Post(() => ApplyTransferProgress(progress));
            }
        }

        private void OnRunProgressChanged(object? sender, DesktopRunProgressSnapshot progress)
        {
            if (_uiDispatcher.CheckAccess())
            {
                if (ShouldQueueVisibleRunProgress(progress))
                {
                    ApplyRunProgress(progress);
                }

                return;
            }

            if (TryPostCoalescedRunProgress(progress))
            {
                return;
            }

            if (ShouldQueueVisibleRunProgress(progress))
            {
                _uiDispatcher.Post(() => ApplyRunProgress(progress));
            }
        }

        private bool TryPostCoalescedTransferProgress(DesktopTransferProgressSnapshot progress)
        {
            lock (_progressDispatchGate)
            {
                if (_pendingCoalescedTransferProgress is not null
                    && CanReplacePendingTransferProgress(_pendingCoalescedTransferProgress, progress))
                {
                    _pendingCoalescedTransferProgress = progress;
                    TrackVisibleTransferProgressUnsafe(progress);
                    return true;
                }

                if (_isCoalescedTransferProgressDispatchScheduled)
                {
                    return false;
                }

                if (!ShouldQueueVisibleTransferProgressUnsafe(progress))
                {
                    return true;
                }

                _pendingCoalescedTransferProgress = progress;
                _isCoalescedTransferProgressDispatchScheduled = true;
            }

            _uiDispatcher.Post(ApplyPendingCoalescedTransferProgress);
            return true;
        }

        private bool TryPostCoalescedRunProgress(DesktopRunProgressSnapshot progress)
        {
            lock (_progressDispatchGate)
            {
                if (_pendingCoalescedRunProgress is not null
                    && CanReplacePendingRunProgress(_pendingCoalescedRunProgress, progress))
                {
                    _pendingCoalescedRunProgress = progress;
                    TrackVisibleRunProgressUnsafe(progress);
                    return true;
                }

                if (_isCoalescedRunProgressDispatchScheduled)
                {
                    return false;
                }

                if (!ShouldQueueVisibleRunProgressUnsafe(progress))
                {
                    return true;
                }

                _pendingCoalescedRunProgress = progress;
                _isCoalescedRunProgressDispatchScheduled = true;
            }

            _uiDispatcher.Post(ApplyPendingCoalescedRunProgress);
            return true;
        }

        private void ApplyPendingCoalescedTransferProgress()
        {
            DesktopTransferProgressSnapshot? progress;
            lock (_progressDispatchGate)
            {
                progress = _pendingCoalescedTransferProgress;
                _pendingCoalescedTransferProgress = null;
                _isCoalescedTransferProgressDispatchScheduled = false;
            }

            if (progress is not null)
            {
                ApplyTransferProgress(progress);
            }
        }

        private void ApplyPendingCoalescedRunProgress()
        {
            DesktopRunProgressSnapshot? progress;
            lock (_progressDispatchGate)
            {
                progress = _pendingCoalescedRunProgress;
                _pendingCoalescedRunProgress = null;
                _isCoalescedRunProgressDispatchScheduled = false;
            }

            if (progress is not null)
            {
                ApplyRunProgress(progress);
            }
        }

        private static bool CanReplacePendingTransferProgress(
            DesktopTransferProgressSnapshot pending,
            DesktopTransferProgressSnapshot next)
        {
            return pending.SyncPairId == next.SyncPairId
                && pending.Direction == next.Direction
                && string.Equals(pending.RelativePath, next.RelativePath, StringComparison.Ordinal)
                && next.OccurredAtUtc >= pending.OccurredAtUtc;
        }

        private bool ShouldQueueVisibleTransferProgress(DesktopTransferProgressSnapshot progress)
        {
            lock (_progressDispatchGate)
            {
                return ShouldQueueVisibleTransferProgressUnsafe(progress);
            }
        }

        private bool ShouldQueueVisibleTransferProgressUnsafe(DesktopTransferProgressSnapshot progress)
        {
            DateTime occurredAtUtc = progress.OccurredAtUtc.ToUniversalTime();
            bool isNewVisibleTransfer = !_visibleTransferSyncPairId.HasValue
                || _visibleTransferSyncPairId.Value != progress.SyncPairId
                || _visibleTransferDirection != progress.Direction
                || !string.Equals(_visibleTransferRelativePath, progress.RelativePath, StringComparison.Ordinal);
            if (isNewVisibleTransfer
                || progress.IsCompleted
                || !_lastVisibleTransferProgressAtUtc.HasValue
                || occurredAtUtc < _lastVisibleTransferProgressAtUtc.Value
                || occurredAtUtc - _lastVisibleTransferProgressAtUtc.Value >= VisibleTransferProgressUpdateInterval)
            {
                TrackVisibleTransferProgressUnsafe(progress);
                return true;
            }

            return false;
        }

        private void TrackVisibleTransferProgressUnsafe(DesktopTransferProgressSnapshot progress)
        {
            _lastVisibleTransferProgressAtUtc = progress.OccurredAtUtc.ToUniversalTime();
            _visibleTransferSyncPairId = progress.SyncPairId;
            _visibleTransferDirection = progress.Direction;
            _visibleTransferRelativePath = progress.RelativePath;
        }

        private static bool CanReplacePendingRunProgress(
            DesktopRunProgressSnapshot pending,
            DesktopRunProgressSnapshot next)
        {
            return pending.SyncPairId == next.SyncPairId
                && pending.Stage == next.Stage
                && next.OccurredAtUtc >= pending.OccurredAtUtc;
        }

        private bool ShouldQueueVisibleRunProgress(DesktopRunProgressSnapshot progress)
        {
            lock (_progressDispatchGate)
            {
                return ShouldQueueVisibleRunProgressUnsafe(progress);
            }
        }

        private bool ShouldQueueVisibleRunProgressUnsafe(DesktopRunProgressSnapshot progress)
        {
            DateTime occurredAtUtc = progress.OccurredAtUtc.ToUniversalTime();
            bool isNewVisibleRunProgress = !_visibleRunProgressSyncPairId.HasValue
                || _visibleRunProgressSyncPairId.Value != progress.SyncPairId
                || _visibleRunProgressStage != progress.Stage;
            if (isNewVisibleRunProgress
                || progress.IsCompleted
                || !_lastVisibleRunProgressAtUtc.HasValue
                || occurredAtUtc < _lastVisibleRunProgressAtUtc.Value
                || occurredAtUtc - _lastVisibleRunProgressAtUtc.Value >= VisibleRunProgressUpdateInterval)
            {
                TrackVisibleRunProgressUnsafe(progress);
                return true;
            }

            return false;
        }

        private void TrackVisibleRunProgressUnsafe(DesktopRunProgressSnapshot progress)
        {
            _lastVisibleRunProgressAtUtc = progress.OccurredAtUtc.ToUniversalTime();
            _visibleRunProgressSyncPairId = progress.SyncPairId;
            _visibleRunProgressStage = progress.Stage;
        }

        private void ApplySessionRevocation(DesktopSessionRevocationSnapshot sessionRevocation)
        {
            if (!IsSignedIn)
            {
                return;
            }

            DateTimeOffset occurredAt = new DateTimeOffset(DateTime.SpecifyKind(sessionRevocation.OccurredAtUtc, DateTimeKind.Utc))
                .ToLocalTime();
            ApplySignedOutState("Session expired");
            AddActivity("Account", string.Empty, "Session revoked by server", occurredAt);
            ShowNativeNotification("Session expired", "Sign in again to continue syncing.");
        }

        private void ApplyActivity(DesktopActivitySnapshot activity)
        {
            DateTimeOffset occurredAt = new DateTimeOffset(DateTime.SpecifyKind(activity.OccurredAtUtc, DateTimeKind.Utc))
                .ToLocalTime();
            AddActivity(
                activity.Kind,
                activity.Path,
                activity.Details,
                occurredAt,
                activity.SyncPairId);
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
            TrackRunTransferProgress(progress);
            if (progress.IsCompleted)
            {
                _transferProgressByPair.Remove(progress.SyncPairId);
            }
            else
            {
                _transferProgressByPair[progress.SyncPairId] = progress;
            }

            syncPair.CurrentOperation = CreateTransferOperation(progress);
            syncPair.HasCurrentProgress = true;
            if (_runProgressByPair.TryGetValue(progress.SyncPairId, out DesktopRunProgressSnapshot? runProgress))
            {
                syncPair.IsCurrentProgressIndeterminate = !runProgress.FilesTotal.HasValue && !runProgress.IsCompleted;
                syncPair.CurrentProgressValue = CalculateRunProgressValue(runProgress);
                RefreshRunProgressSummary();
            }
            else
            {
                syncPair.IsCurrentProgressIndeterminate = IsCurrentTransferIndeterminate;
                syncPair.CurrentProgressValue = CurrentTransferProgressValue;
                RaiseCurrentWorkProgressProperties();
            }

            RefreshCurrentProgressText();
        }

        private void ApplyRunProgress(DesktopRunProgressSnapshot progress)
        {
            SyncPairRowViewModel? syncPair = SyncPairs.FirstOrDefault(pair => pair.Id == progress.SyncPairId);
            if (syncPair is null || progress.Stage == SyncRunProgressStage.Unknown)
            {
                return;
            }

            if (progress.IsCompleted)
            {
                _runProgressByPair.Remove(progress.SyncPairId);
                _runProgressAppliedAtUtcByPair.Remove(progress.SyncPairId);
                if (!HasCurrentTransfer || _transferSyncPairId != progress.SyncPairId)
                {
                    ClearSyncPairProgress(syncPair);
                }

                RefreshRunProgressSummary();
                RefreshCurrentProgressText();
                return;
            }

            _runProgressByPair[progress.SyncPairId] = progress;
            _runProgressAppliedAtUtcByPair[progress.SyncPairId] = DateTime.UtcNow;
            bool hasActiveTransferForPair = HasCurrentTransfer && _transferSyncPairId == progress.SyncPairId;
            if (!hasActiveTransferForPair)
            {
                syncPair.CurrentOperation = CreateRunProgressOperation(progress);
            }

            syncPair.HasCurrentProgress = true;
            syncPair.IsCurrentProgressIndeterminate = !progress.FilesTotal.HasValue && !progress.IsCompleted;
            syncPair.CurrentProgressValue = CalculateRunProgressValue(progress);
            RefreshRunProgressSummary();
            RefreshCurrentProgressText();
        }

        private void ApplyStatus(DesktopSyncStatusSnapshot status)
        {
            bool hasActiveSyncStatus = false;
            bool runProgressChanged = false;
            bool shouldClearCurrentTransfer = false;
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
                bool isActiveStatus = IsActiveSyncStatus(pairStatus);
                bool hasFreshDetailedProgress = HasFreshDetailedProgress(pairStatus.Id);
                if (!isActiveStatus || !hasFreshDetailedProgress)
                {
                    row.CurrentOperation = pairStatus.CurrentOperation ?? string.Empty;
                }

                hasActiveSyncStatus |= isActiveStatus;
                if (isActiveStatus)
                {
                    EnsureSyncPairProgress(row);
                }
                else
                {
                    ClearSyncPairProgress(row);
                    runProgressChanged |= _runProgressByPair.Remove(pairStatus.Id);
                    _runProgressAppliedAtUtcByPair.Remove(pairStatus.Id);
                    _transferProgressByPair.Remove(pairStatus.Id);
                    shouldClearCurrentTransfer |= _transferSyncPairId == pairStatus.Id;
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
            OnPropertyChanged(nameof(IsStatusCardVisible));
            OnPropertyChanged(nameof(HasDashboardNotifications));
            if (!hasActiveSyncStatus)
            {
                ClearTransferProgress();
                ClearRunProgress();
            }
            else
            {
                if (shouldClearCurrentTransfer)
                {
                    ClearTransferProgress();
                }

                if (runProgressChanged)
                {
                    RefreshRunProgressSummary();
                }
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

        private bool HasFreshDetailedProgress(Guid syncPairId)
        {
            if (_transferProgressByPair.ContainsKey(syncPairId)
                || (_transferSyncPairId == syncPairId && HasCurrentTransfer))
            {
                return true;
            }

            if (!_runProgressByPair.ContainsKey(syncPairId))
            {
                return false;
            }

            if (!_runProgressAppliedAtUtcByPair.TryGetValue(syncPairId, out DateTime appliedAtUtc)
                || DateTime.UtcNow - appliedAtUtc.ToUniversalTime() <= ActiveStatusRunProgressStaleThreshold)
            {
                return true;
            }

            _runProgressByPair.Remove(syncPairId);
            _runProgressAppliedAtUtcByPair.Remove(syncPairId);
            RefreshRunProgressSummary();
            return false;
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
            row.CurrentOperation = string.Empty;
            row.HasCurrentProgress = false;
            row.IsCurrentProgressIndeterminate = false;
            row.CurrentProgressValue = 0;
        }

        private void AddNotifications(IReadOnlyList<DesktopNotificationRequest> requests)
        {
            foreach (DesktopNotificationRequest request in requests)
            {
                if (Notifications.FirstOrDefault() is { } latest
                    && string.Equals(latest.Title, request.Title, StringComparison.Ordinal)
                    && string.Equals(latest.Message, request.Message, StringComparison.Ordinal))
                {
                    continue;
                }

                Notifications.Insert(0, new NotificationRowViewModel
                {
                    Title = request.Title,
                    Message = request.Message,
                    IsDashboardVisible = IsDashboardNotificationKind(request.Kind),
                });
                AddActivity("Notification", string.Empty, request.Message);
                if (EnableNotifications && _notificationService.IsSupported)
                {
                    _notificationService.Show(request.Title, request.Message);
                }
            }

            while (Notifications.Count > 3)
            {
                Notifications.RemoveAt(Notifications.Count - 1);
            }
        }

        private static bool IsDashboardNotificationKind(DesktopNotificationKind kind)
        {
            return kind != DesktopNotificationKind.InitialSyncComplete
                && kind != DesktopNotificationKind.ActionRequiredError;
        }

        private void ShowNativeNotification(string title, string message)
        {
            if (EnableNotifications && _notificationService.IsSupported)
            {
                _notificationService.Show(
                    DesktopUserMessageFormatter.Compact(title, DesktopUserMessageFormatter.TitleMaxLength),
                    DesktopUserMessageFormatter.Compact(message));
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

        private void AddActivity(string kind, string path, string details, DateTimeOffset occurredAt, Guid? syncPairId = null)
        {
            ActivityRowViewModel row = CreateActivityRow(kind, path, details, occurredAt);
            if (ShouldCoalesceActivity(kind, syncPairId, occurredAt))
            {
                Activities[0] = row;
                _lastCoalescedActivityAt = occurredAt;
                return;
            }

            Activities.Insert(0, row);
            TrackCoalescibleActivity(kind, syncPairId, occurredAt);
            while (Activities.Count > MaxActivityRows)
            {
                Activities.RemoveAt(Activities.Count - 1);
            }
        }

        private static ActivityRowViewModel CreateActivityRow(string kind, string path, string details, DateTimeOffset occurredAt)
        {
            return new ActivityRowViewModel
            {
                Time = occurredAt.ToString("HH:mm", CultureInfo.CurrentCulture),
                Kind = kind,
                Path = path,
                Details = string.IsNullOrWhiteSpace(details)
                    ? string.Empty
                    : DesktopUserMessageFormatter.Compact(details),
            };
        }

        private bool ShouldCoalesceActivity(string kind, Guid? syncPairId, DateTimeOffset occurredAt)
        {
            if (!IsHighVolumeTransferActivity(kind)
                || Activities.Count == 0
                || !_lastCoalescedActivityAt.HasValue
                || !Equals(_lastCoalescedActivitySyncPairId, syncPairId))
            {
                return false;
            }

            ActivityRowViewModel latest = Activities[0];
            return string.Equals(latest.Kind, kind, StringComparison.Ordinal)
                && occurredAt >= _lastCoalescedActivityAt.Value
                && occurredAt - _lastCoalescedActivityAt.Value <= TransferActivityCoalescingWindow;
        }

        private void TrackCoalescibleActivity(string kind, Guid? syncPairId, DateTimeOffset occurredAt)
        {
            if (!IsHighVolumeTransferActivity(kind))
            {
                _lastCoalescedActivityAt = null;
                _lastCoalescedActivitySyncPairId = null;
                return;
            }

            _lastCoalescedActivityAt = occurredAt;
            _lastCoalescedActivitySyncPairId = syncPairId;
        }

        private static bool IsHighVolumeTransferActivity(string kind)
        {
            return string.Equals(kind, "Uploaded", StringComparison.Ordinal)
                || string.Equals(kind, "Downloaded", StringComparison.Ordinal)
                || string.Equals(kind, "Deleted local copy", StringComparison.Ordinal)
                || string.Equals(kind, "Deleted remote copy", StringComparison.Ordinal);
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
            SignInWithBrowserCommand.RaiseCanExecuteChanged();
            CancelBrowserSignInCommand.RaiseCanExecuteChanged();
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
            ToggleActivityCommand.RaiseCanExecuteChanged();
            ChangeSelectedSyncPairLocalFolderCommand.RaiseCanExecuteChanged();
            ChangeSelectedSyncPairRemoteFolderCommand.RaiseCanExecuteChanged();
            ToggleSelectedSyncPairEnabledCommand.RaiseCanExecuteChanged();
            SaveSelectedSyncPairNameCommand.RaiseCanExecuteChanged();
            RemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
            ShowSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
            ConfirmRemoveSelectedSyncPairCommand.RaiseCanExecuteChanged();
            CancelSelectedSyncPairEditorCommand.RaiseCanExecuteChanged();
            OpenWebCommand.RaiseCanExecuteChanged();
            ShowAddSyncPairCommand.RaiseCanExecuteChanged();
            ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
            UseRemoteFolderCommand.RaiseCanExecuteChanged();
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
            SyncNowCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            PauseResumeCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanSyncNow));
            OnPropertyChanged(nameof(CanPauseSync));
            OnPropertyChanged(nameof(CanResumeSync));
            OnPropertyChanged(nameof(CanTogglePauseResumeSync));
            OnPropertyChanged(nameof(PauseResumeSyncLabel));
            OnPropertyChanged(nameof(PauseResumeTrayLabel));
            OnPropertyChanged(nameof(IsSyncPaused));
            OnPropertyChanged(nameof(HasStatusAttention));
            OnPropertyChanged(nameof(IsStatusCardVisible));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(StatusCardTitle));
            OnPropertyChanged(nameof(StatusCardDetailText));
            OnPropertyChanged(nameof(HasStatusCardDetail));
            OnPropertyChanged(nameof(HasDashboardNotifications));
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
            UseRemoteFolderCommand.RaiseCanExecuteChanged();
            ChangeSelectedSyncPairRemoteFolderCommand.RaiseCanExecuteChanged();
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

        private void SetSnapshotLoading(bool isLoading)
        {
            if (SetProperty(ref _isLoadingSnapshot, isLoading, nameof(IsStartupLoadingVisible)))
            {
                OnPropertyChanged(nameof(IsDashboardVisible));
                OnPropertyChanged(nameof(IsDashboardHeaderVisible));
                OnPropertyChanged(nameof(IsSetupVisible));
                RaiseSetupStateProperties();
                RaiseCommandStates();
            }
        }

        private void RaiseWizardStateProperties()
        {
            OnPropertyChanged(nameof(HasLocalFolderSelection));
            OnPropertyChanged(nameof(IsAddSyncPairLocalStepVisible));
            OnPropertyChanged(nameof(IsAddSyncPairCloudStepVisible));
            OnPropertyChanged(nameof(IsCreateRemoteFolderVisible));
            OnPropertyChanged(nameof(AddSyncPairWizardTitle));
            OnPropertyChanged(nameof(AddSyncPairWizardSubtitle));
            OnPropertyChanged(nameof(IsAddSyncPairLocalSummaryVisible));
            OnPropertyChanged(nameof(RemoteFolderWizardPrimaryActionText));
            OnPropertyChanged(nameof(RemoteFolderWizardPrimaryActionToolTip));
            ShowCreateRemoteFolderCommand.RaiseCanExecuteChanged();
            CreateRemoteFolderCommand.RaiseCanExecuteChanged();
            UseRemoteFolderCommand.RaiseCanExecuteChanged();
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

            if (HasPairStatusAttention)
            {
                CurrentProgressText = "Fix the folder issue to continue syncing.";
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
            lock (_progressDispatchGate)
            {
                _pendingCoalescedTransferProgress = null;
                _isCoalescedTransferProgressDispatchScheduled = false;
                _lastVisibleTransferProgressAtUtc = null;
                _visibleTransferSyncPairId = null;
                _visibleTransferDirection = SyncTransferDirection.Unknown;
                _visibleTransferRelativePath = string.Empty;
            }

            HasCurrentTransfer = false;
            IsCurrentTransferIndeterminate = false;
            CurrentTransferProgressValue = 0;
            CurrentTransferTitle = string.Empty;
            CurrentTransferDetails = string.Empty;
            _transferProgressByPair.Clear();
            _transferSyncPairId = null;
            _transferDirection = SyncTransferDirection.Unknown;
            _transferRelativePath = string.Empty;
            RaiseCurrentWorkProgressProperties();
        }

        private void ClearRunProgress()
        {
            _runProgressByPair.Clear();
            _runProgressAppliedAtUtcByPair.Clear();
            ClearRunTransferMetrics();
            lock (_progressDispatchGate)
            {
                _pendingCoalescedRunProgress = null;
                _isCoalescedRunProgressDispatchScheduled = false;
                _lastVisibleRunProgressAtUtc = null;
                _visibleRunProgressSyncPairId = null;
                _visibleRunProgressStage = SyncRunProgressStage.Unknown;
            }

            HasCurrentRunProgress = false;
            IsCurrentRunProgressIndeterminate = false;
            CurrentRunProgressValue = 0;
            CurrentRunProgressTitle = string.Empty;
            CurrentRunProgressDetails = string.Empty;
            RaiseCurrentWorkProgressProperties();
        }

        private void RefreshRunProgressSummary(bool updateEstimate = true)
        {
            List<DesktopRunProgressSnapshot> progressValues = GetOrderedRunProgressSnapshots();
            if (progressValues.Count == 0)
            {
                ClearRunProgress();
                return;
            }

            HasCurrentRunProgress = true;
            if (updateEstimate)
            {
                UpdateRunProgressEstimatedTimeRemaining(progressValues);
                UpdateRunTransferEstimatedTimeRemaining(progressValues);
            }

            if (progressValues.Count == 1)
            {
                DesktopRunProgressSnapshot progress = progressValues[0];
                SyncPairRowViewModel syncPair = SyncPairs.First(pair => pair.Id == progress.SyncPairId);
                IsCurrentRunProgressIndeterminate = !progress.FilesTotal.HasValue && !progress.IsCompleted;
                CurrentRunProgressValue = CalculateRunProgressValue(progress);
                CurrentRunProgressTitle = syncPair.DisplayName;
                CurrentRunProgressDetails = CreateSingleRunProgressDetails(progress);
                RaiseCurrentWorkProgressProperties();
                return;
            }

            IsCurrentRunProgressIndeterminate = progressValues.Any(static progress => !progress.FilesTotal.HasValue && !progress.IsCompleted);
            CurrentRunProgressValue = CalculateAggregateRunProgressValue(progressValues);
            CurrentRunProgressTitle = "Syncing " + progressValues.Count.ToString(CultureInfo.CurrentCulture) + " folders";
            CurrentRunProgressDetails = CreateAggregateRunProgressDetails(progressValues);
            RaiseCurrentWorkProgressProperties();
        }

        private List<DesktopRunProgressSnapshot> GetOrderedRunProgressSnapshots()
        {
            var progressValues = new List<DesktopRunProgressSnapshot>();
            foreach (SyncPairRowViewModel syncPair in SyncPairs)
            {
                if (_runProgressByPair.TryGetValue(syncPair.Id, out DesktopRunProgressSnapshot? progress))
                {
                    progressValues.Add(progress);
                }
            }

            return progressValues;
        }

        private void RaiseCurrentWorkProgressProperties()
        {
            OnPropertyChanged(nameof(HasCurrentWorkProgress));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(IsStatusCardVisible));
            OnPropertyChanged(nameof(HasDashboardNotifications));
            OnPropertyChanged(nameof(CurrentWorkProgressTitle));
            OnPropertyChanged(nameof(CurrentWorkProgressHeaderDetails));
            OnPropertyChanged(nameof(HasCurrentWorkProgressHeaderDetails));
            OnPropertyChanged(nameof(CurrentWorkProgressHeaderSizeDetails));
            OnPropertyChanged(nameof(HasCurrentWorkProgressHeaderSizeDetails));
            OnPropertyChanged(nameof(CurrentWorkProgressHeaderRateDetails));
            OnPropertyChanged(nameof(HasCurrentWorkProgressHeaderRateDetails));
            OnPropertyChanged(nameof(CurrentWorkProgressDetails));
            OnPropertyChanged(nameof(CurrentWorkProgressSecondaryDetails));
            OnPropertyChanged(nameof(HasCurrentWorkProgressSecondaryDetails));
            OnPropertyChanged(nameof(CurrentWorkProgressValue));
            OnPropertyChanged(nameof(IsCurrentWorkProgressIndeterminate));
        }

        private string CreateRunTransferSizeDetails()
        {
            if (TryCalculateAggregateRunTransferBytes(out long transferredBytes, out long totalBytes))
            {
                return FormatBytes(transferredBytes) + " / " + FormatBytes(totalBytes);
            }

            if (_runTransferredBytes > 0)
            {
                return FormatBytes(_runTransferredBytes);
            }

            return HasActiveTransferProgress
                ? CreateAggregateTransferMetricDetails(_transferProgressByPair.Values).Size
                : string.Empty;
        }

        private string CreateRunTransferRateDetails()
        {
            var parts = new List<string>();
            bool hasAggregateTransferBytes = TryCalculateAggregateRunTransferBytes(
                out long transferredBytes,
                out long totalBytes);
            bool hasByteRate = false;
            bool hasByteEstimate = false;
            if (hasAggregateTransferBytes && TryGetRunTransferSpeed(out double bytesPerSecond))
            {
                parts.Add(FormatBytes(bytesPerSecond) + "/s");
                hasByteRate = true;
                if (totalBytes > transferredBytes)
                {
                    TimeSpan estimatedTimeRemaining = _runTransferEstimatedTimeRemaining
                        ?? TimeSpan.FromSeconds((totalBytes - transferredBytes) / bytesPerSecond);
                    parts.Add(FormatDuration(estimatedTimeRemaining) + " left");
                    hasByteEstimate = true;
                }
            }
            else if (HasActiveTransferProgress && !hasAggregateTransferBytes)
            {
                string activeTransferRate = CreateAggregateTransferMetricDetails(
                    _transferProgressByPair.Values,
                    includeEstimatedTimeRemaining: false).Rate;
                if (!string.IsNullOrWhiteSpace(activeTransferRate))
                {
                    parts.Add(activeTransferRate);
                    hasByteRate = true;
                }
            }
            else if (_runTransferSpeedBytesPerSecond is > 0)
            {
                parts.Add(FormatBytes(_runTransferSpeedBytesPerSecond.Value) + "/s");
                hasByteRate = true;
            }

            if (!hasByteRate && _currentRunProgressFilesPerSecond is > 0)
            {
                parts.Add(FormatFileRate(_currentRunProgressFilesPerSecond.Value));
            }

            if (!hasByteEstimate && _currentRunProgressEstimatedTimeRemaining.HasValue)
            {
                parts.Add(FormatDuration(_currentRunProgressEstimatedTimeRemaining.Value) + " left");
            }

            return string.Join(" · ", parts);
        }

        private void ClearRunTransferMetrics()
        {
            _runCompletedTransferBytesByPair.Clear();
            _runCompletedTransferBytesByKey.Clear();
            _runTransferBytesByKey.Clear();
            _runFileProgressSamples.Clear();
            _runTransferSamples.Clear();
            _runTransferredBytes = 0;
            _runTransferSpeedBytesPerSecond = null;
            _runTransferEstimatedTimeRemaining = null;
            _lastRunTransferSpeedOccurredAtUtc = null;
            _lastRunTransferEstimateOccurredAtUtc = null;
            _currentRunProgressFilesPerSecond = null;
            _currentRunProgressEstimatedTimeRemaining = null;
            _lastRunProgressFileRateOccurredAtUtc = null;
            _lastRunProgressEstimateOccurredAtUtc = null;
        }

        private void TrackRunTransferProgress(DesktopTransferProgressSnapshot progress)
        {
            if (!IsRunTransferDirection(progress.Direction))
            {
                return;
            }

            long effectiveTransferredBytes = progress.IsCompleted && progress.TotalBytes.HasValue
                ? progress.TotalBytes.Value
                : progress.TransferredBytes;
            effectiveTransferredBytes = Math.Max(0, effectiveTransferredBytes);

            var key = new RunTransferProgressKey(progress.SyncPairId, progress.Direction, progress.RelativePath);
            _runTransferBytesByKey.TryGetValue(key, out long previousTransferredBytes);
            if (effectiveTransferredBytes < previousTransferredBytes)
            {
                previousTransferredBytes = 0;
            }

            if (progress.IsCompleted)
            {
                _runTransferBytesByKey.Remove(key);
                TrackCompletedRunTransferBytes(key, effectiveTransferredBytes);
            }
            else
            {
                _runTransferBytesByKey[key] = effectiveTransferredBytes;
            }

            long transferredDelta = effectiveTransferredBytes - previousTransferredBytes;
            if (transferredDelta <= 0)
            {
                return;
            }

            _runTransferredBytes += transferredDelta;
            AddRunTransferSample(_runTransferredBytes, progress.OccurredAtUtc);
        }

        private void TrackCompletedRunTransferBytes(RunTransferProgressKey key, long completedBytes)
        {
            if (completedBytes <= 0)
            {
                return;
            }

            _runCompletedTransferBytesByKey.TryGetValue(key, out long existingCompletedBytes);
            if (completedBytes > existingCompletedBytes)
            {
                _runCompletedTransferBytesByKey[key] = completedBytes;
                long completedBytesDelta = completedBytes - existingCompletedBytes;
                _runCompletedTransferBytesByPair.TryGetValue(key.SyncPairId, out long pairCompletedBytes);
                _runCompletedTransferBytesByPair[key.SyncPairId] = pairCompletedBytes + completedBytesDelta;
            }
        }

        private void AddRunTransferSample(long transferredBytes, DateTime occurredAtUtc)
        {
            if (_runTransferSamples.Count == 0 && transferredBytes <= 0)
            {
                return;
            }

            if (_runTransferSamples.Count > 0
                && occurredAtUtc - _runTransferSamples.Last().OccurredAtUtc > RunTransferMetricsWindow)
            {
                _runTransferSamples.Clear();
                _runTransferSpeedBytesPerSecond = null;
                _lastRunTransferSpeedOccurredAtUtc = null;
            }

            if (_runTransferSamples.Count == 0)
            {
                _runTransferSamples.Enqueue(new RunTransferProgressSample(transferredBytes, occurredAtUtc));
                return;
            }

            RunTransferProgressSample lastSample = _runTransferSamples.Last();
            if (occurredAtUtc == lastSample.OccurredAtUtc)
            {
                if (transferredBytes > lastSample.TransferredBytes)
                {
                    ReplaceLastRunTransferSample(new RunTransferProgressSample(transferredBytes, occurredAtUtc));
                    UpdateRunTransferSpeedFromSamples();
                }

                return;
            }

            if (occurredAtUtc < lastSample.OccurredAtUtc)
            {
                return;
            }

            if (transferredBytes < _runTransferSamples.Last().TransferredBytes)
            {
                _runTransferSamples.Clear();
                _runTransferSpeedBytesPerSecond = null;
                _lastRunTransferSpeedOccurredAtUtc = null;
                _runTransferSamples.Enqueue(new RunTransferProgressSample(transferredBytes, occurredAtUtc));
                return;
            }

            _runTransferSamples.Enqueue(new RunTransferProgressSample(transferredBytes, occurredAtUtc));
            PruneRunTransferSamples(occurredAtUtc);
            UpdateRunTransferSpeedFromSamples();
        }

        private void ReplaceLastRunTransferSample(RunTransferProgressSample sample)
        {
            RunTransferProgressSample[] samples = _runTransferSamples.ToArray();
            _runTransferSamples.Clear();
            for (int index = 0; index < samples.Length - 1; index++)
            {
                _runTransferSamples.Enqueue(samples[index]);
            }

            _runTransferSamples.Enqueue(sample);
        }

        private void PruneRunTransferSamples(DateTime occurredAtUtc)
        {
            while (_runTransferSamples.Count > 2
                && occurredAtUtc - _runTransferSamples.Peek().OccurredAtUtc > RunTransferMetricsWindow)
            {
                _runTransferSamples.Dequeue();
            }
        }

        private void UpdateRunTransferSpeedFromSamples()
        {
            if (_runTransferSamples.Count < 2)
            {
                return;
            }

            RunTransferProgressSample firstSample = _runTransferSamples.Peek();
            RunTransferProgressSample lastSample = _runTransferSamples.Last();
            TimeSpan elapsed = lastSample.OccurredAtUtc - firstSample.OccurredAtUtc;
            long transferredBytes = lastSample.TransferredBytes - firstSample.TransferredBytes;
            if (elapsed < MinimumRunTransferSampleDuration || transferredBytes <= 0)
            {
                return;
            }

            double observedBytesPerSecond = transferredBytes / elapsed.TotalSeconds;
            UpdateRunTransferSpeed(observedBytesPerSecond, lastSample.OccurredAtUtc);
        }

        private void UpdateRunTransferSpeed(double observedBytesPerSecond, DateTime occurredAtUtc)
        {
            if (!_runTransferSpeedBytesPerSecond.HasValue
                || !_lastRunTransferSpeedOccurredAtUtc.HasValue
                || occurredAtUtc <= _lastRunTransferSpeedOccurredAtUtc.Value)
            {
                _runTransferSpeedBytesPerSecond = observedBytesPerSecond;
                _lastRunTransferSpeedOccurredAtUtc = occurredAtUtc;
                return;
            }

            TimeSpan sampleElapsed = occurredAtUtc - _lastRunTransferSpeedOccurredAtUtc.Value;
            double smoothingFactor = CalculateExponentialSmoothingFactor(sampleElapsed, RunProgressEstimateSmoothingPeriod);
            _runTransferSpeedBytesPerSecond = Math.Max(
                0,
                _runTransferSpeedBytesPerSecond.Value
                    + ((observedBytesPerSecond - _runTransferSpeedBytesPerSecond.Value) * smoothingFactor));
            _lastRunTransferSpeedOccurredAtUtc = occurredAtUtc;
        }

        private void UpdateRunProgressEstimatedTimeRemaining(IReadOnlyList<DesktopRunProgressSnapshot> progressValues)
        {
            if (!TryCalculateAggregateRunProgressEstimate(
                progressValues,
                out double observedFilesPerSecond,
                out double remainingFiles,
                out DateTime occurredAtUtc))
            {
                _currentRunProgressFilesPerSecond = null;
                _currentRunProgressEstimatedTimeRemaining = null;
                _lastRunProgressFileRateOccurredAtUtc = null;
                _lastRunProgressEstimateOccurredAtUtc = null;
                return;
            }

            UpdateRunFileRate(observedFilesPerSecond, occurredAtUtc);
            TimeSpan? rawEstimatedTimeRemaining = _currentRunProgressFilesPerSecond is > 0
                ? TimeSpan.FromSeconds(remainingFiles / _currentRunProgressFilesPerSecond.Value)
                : null;
            _currentRunProgressEstimatedTimeRemaining = rawEstimatedTimeRemaining.HasValue
                ? SmoothEstimatedTimeRemaining(
                    rawEstimatedTimeRemaining.Value,
                    occurredAtUtc,
                    _currentRunProgressEstimatedTimeRemaining,
                    _lastRunProgressEstimateOccurredAtUtc)
                : null;
            _lastRunProgressEstimateOccurredAtUtc = rawEstimatedTimeRemaining.HasValue ? occurredAtUtc : null;
        }

        private void UpdateRunTransferEstimatedTimeRemaining(IReadOnlyList<DesktopRunProgressSnapshot> progressValues)
        {
            if (!TryCalculateAggregateRunTransferBytes(progressValues, out long transferredBytes, out long totalBytes)
                || transferredBytes >= totalBytes)
            {
                _runTransferEstimatedTimeRemaining = null;
                _lastRunTransferEstimateOccurredAtUtc = null;
                return;
            }

            DateTime occurredAtUtc = GetLatestRunTransferEstimateOccurredAtUtc(progressValues);
            if (transferredBytes > _runTransferredBytes)
            {
                _runTransferredBytes = transferredBytes;
            }

            AddRunTransferSample(_runTransferredBytes, occurredAtUtc);
            if (!TryGetRunTransferSpeed(out double bytesPerSecond))
            {
                _runTransferEstimatedTimeRemaining = null;
                _lastRunTransferEstimateOccurredAtUtc = null;
                return;
            }

            TimeSpan rawEstimatedTimeRemaining = TimeSpan.FromSeconds((totalBytes - transferredBytes) / bytesPerSecond);
            _runTransferEstimatedTimeRemaining = SmoothEstimatedTimeRemaining(
                rawEstimatedTimeRemaining,
                occurredAtUtc,
                _runTransferEstimatedTimeRemaining,
                _lastRunTransferEstimateOccurredAtUtc);
            _lastRunTransferEstimateOccurredAtUtc = occurredAtUtc;
        }

        private DateTime GetLatestRunTransferEstimateOccurredAtUtc(IReadOnlyList<DesktopRunProgressSnapshot> progressValues)
        {
            DateTime occurredAtUtc = _runTransferSamples.Count > 0
                ? _runTransferSamples.Last().OccurredAtUtc
                : DateTime.MinValue;
            foreach (DesktopRunProgressSnapshot progress in progressValues)
            {
                DateTime progressOccurredAtUtc = progress.OccurredAtUtc.ToUniversalTime();
                if (progressOccurredAtUtc > occurredAtUtc)
                {
                    occurredAtUtc = progressOccurredAtUtc;
                }
            }

            return occurredAtUtc;
        }

        private static TimeSpan SmoothEstimatedTimeRemaining(
            TimeSpan rawEstimate,
            DateTime occurredAtUtc,
            TimeSpan? previousEstimate,
            DateTime? previousOccurredAtUtc)
        {
            if (!previousEstimate.HasValue
                || !previousOccurredAtUtc.HasValue
                || occurredAtUtc <= previousOccurredAtUtc.Value)
            {
                return rawEstimate;
            }

            TimeSpan elapsed = occurredAtUtc - previousOccurredAtUtc.Value;
            TimeSpan agedPreviousEstimate = previousEstimate.Value - elapsed;
            if (agedPreviousEstimate < TimeSpan.Zero)
            {
                agedPreviousEstimate = TimeSpan.Zero;
            }

            double smoothingFactor = CalculateExponentialSmoothingFactor(elapsed, RunProgressEstimateSmoothingPeriod);
            double smoothedSeconds = agedPreviousEstimate.TotalSeconds
                + ((rawEstimate.TotalSeconds - agedPreviousEstimate.TotalSeconds) * smoothingFactor);
            return TimeSpan.FromSeconds(Math.Max(0, smoothedSeconds));
        }

        private bool TryCalculateAggregateRunProgressEstimate(
            IReadOnlyList<DesktopRunProgressSnapshot> progressValues,
            out double filesPerSecond,
            out double remainingFiles,
            out DateTime occurredAtUtc)
        {
            filesPerSecond = 0;
            remainingFiles = 0;
            occurredAtUtc = DateTime.MinValue;
            double completedFiles = 0;
            int totalFiles = 0;
            DateTime latestRunProgressAtUtc = DateTime.MinValue;
            foreach (DesktopRunProgressSnapshot progress in progressValues)
            {
                if (!IsCountedRunStage(progress.Stage) || progress.FilesTotal is not > 0)
                {
                    continue;
                }

                completedFiles += Math.Clamp(progress.FilesCompleted, 0, progress.FilesTotal.Value);
                totalFiles += progress.FilesTotal.Value;
                DateTime progressOccurredAtUtc = progress.OccurredAtUtc.ToUniversalTime();
                if (progressOccurredAtUtc > latestRunProgressAtUtc)
                {
                    latestRunProgressAtUtc = progressOccurredAtUtc;
                }
            }

            if (totalFiles <= 0 || completedFiles >= totalFiles || latestRunProgressAtUtc == DateTime.MinValue)
            {
                return false;
            }

            occurredAtUtc = latestRunProgressAtUtc;
            remainingFiles = totalFiles - completedFiles;
            if (!TryCalculateRunFileRate(totalFiles, completedFiles, occurredAtUtc, out filesPerSecond))
            {
                return false;
            }

            return remainingFiles > 0;
        }

        private void UpdateRunFileRate(double observedFilesPerSecond, DateTime occurredAtUtc)
        {
            if (!double.IsFinite(observedFilesPerSecond) || observedFilesPerSecond <= 0)
            {
                return;
            }

            if (!_currentRunProgressFilesPerSecond.HasValue
                || !_lastRunProgressFileRateOccurredAtUtc.HasValue
                || occurredAtUtc <= _lastRunProgressFileRateOccurredAtUtc.Value)
            {
                _currentRunProgressFilesPerSecond = observedFilesPerSecond;
                _lastRunProgressFileRateOccurredAtUtc = occurredAtUtc;
                return;
            }

            TimeSpan sampleElapsed = occurredAtUtc - _lastRunProgressFileRateOccurredAtUtc.Value;
            double smoothingFactor = CalculateExponentialSmoothingFactor(sampleElapsed, RunProgressEstimateSmoothingPeriod);
            _currentRunProgressFilesPerSecond = Math.Max(
                0,
                _currentRunProgressFilesPerSecond.Value
                    + ((observedFilesPerSecond - _currentRunProgressFilesPerSecond.Value) * smoothingFactor));
            _lastRunProgressFileRateOccurredAtUtc = occurredAtUtc;
        }

        private bool TryCalculateRunFileRate(
            int totalFiles,
            double completedFiles,
            DateTime occurredAtUtc,
            out double filesPerSecond)
        {
            filesPerSecond = 0;
            if (_runFileProgressSamples.Count > 0)
            {
                RunFileProgressSample lastSample = _runFileProgressSamples.Last();
                if (totalFiles < lastSample.TotalFiles
                    || completedFiles < lastSample.CompletedFiles
                    || occurredAtUtc - lastSample.OccurredAtUtc > RunTransferMetricsWindow)
                {
                    _runFileProgressSamples.Clear();
                }
                else if (completedFiles == lastSample.CompletedFiles)
                {
                    return TryCalculateRunFileRateFromSamples(out filesPerSecond);
                }
            }

            _runFileProgressSamples.Enqueue(new RunFileProgressSample(completedFiles, totalFiles, occurredAtUtc));
            PruneRunFileProgressSamples(occurredAtUtc);
            if (completedFiles < MinimumRunProgressEstimateCompletedFiles)
            {
                return false;
            }

            return TryCalculateRunFileRateFromSamples(out filesPerSecond);
        }

        private void PruneRunFileProgressSamples(DateTime occurredAtUtc)
        {
            while (_runFileProgressSamples.Count > 2
                && occurredAtUtc - _runFileProgressSamples.Peek().OccurredAtUtc > RunTransferMetricsWindow)
            {
                _runFileProgressSamples.Dequeue();
            }
        }

        private bool TryCalculateRunFileRateFromSamples(out double filesPerSecond)
        {
            filesPerSecond = 0;
            if (_runFileProgressSamples.Count < 2)
            {
                return false;
            }

            RunFileProgressSample firstSample = _runFileProgressSamples.Peek();
            RunFileProgressSample lastSample = _runFileProgressSamples.Last();
            if (lastSample.CompletedFiles < MinimumRunProgressEstimateCompletedFiles)
            {
                return false;
            }

            TimeSpan elapsed = lastSample.OccurredAtUtc - firstSample.OccurredAtUtc;
            double completedFiles = lastSample.CompletedFiles - firstSample.CompletedFiles;
            if (elapsed < MinimumRunProgressEstimateDuration || completedFiles <= 0)
            {
                return false;
            }

            filesPerSecond = completedFiles / elapsed.TotalSeconds;
            return double.IsFinite(filesPerSecond) && filesPerSecond > 0;
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

        private double CalculateRunProgressValue(DesktopRunProgressSnapshot progress)
        {
            if (TryCalculateRunTransferBytes(progress, out long transferredBytes, out long totalBytes))
            {
                return Math.Clamp((double)transferredBytes / totalBytes * 100, 0, 100);
            }

            if (progress.FilesTotal is > 0)
            {
                double displayCount = GetDisplayedRunProgressUnits(progress);
                return Math.Clamp(displayCount / progress.FilesTotal.Value * 100, 0, 100);
            }

            return progress.IsCompleted ? 100 : 0;
        }

        private double CalculateAggregateRunProgressValue(IReadOnlyList<DesktopRunProgressSnapshot> progressValues)
        {
            if (TryCalculateAggregateRunTransferBytes(progressValues, out long transferredBytes, out long totalBytes))
            {
                return Math.Clamp((double)transferredBytes / totalBytes * 100, 0, 100);
            }

            int totalFiles = 0;
            double completedFiles = 0;
            foreach (DesktopRunProgressSnapshot progress in progressValues)
            {
                if (!progress.FilesTotal.HasValue)
                {
                    return progressValues.All(static item => item.IsCompleted) ? 100 : 0;
                }

                totalFiles += progress.FilesTotal.Value;
                completedFiles += GetDisplayedRunProgressUnits(progress);
            }

            return totalFiles > 0
                ? Math.Clamp(completedFiles / totalFiles * 100, 0, 100)
                : progressValues.All(static item => item.IsCompleted) ? 100 : 0;
        }

        private bool TryCalculateAggregateRunTransferBytes(out long transferredBytes, out long totalBytes)
        {
            return TryCalculateAggregateRunTransferBytes(
                GetOrderedRunProgressSnapshots(),
                out transferredBytes,
                out totalBytes);
        }

        private bool TryCalculateAggregateRunTransferBytes(
            IReadOnlyList<DesktopRunProgressSnapshot> progressValues,
            out long transferredBytes,
            out long totalBytes)
        {
            transferredBytes = 0;
            totalBytes = 0;
            foreach (DesktopRunProgressSnapshot progress in progressValues)
            {
                if (!TryCalculateRunTransferBytes(progress, out long progressTransferredBytes, out long progressTotalBytes))
                {
                    continue;
                }

                transferredBytes += progressTransferredBytes;
                totalBytes += progressTotalBytes;
            }

            if (totalBytes <= 0)
            {
                transferredBytes = 0;
                return false;
            }

            transferredBytes = Math.Clamp(transferredBytes, 0, totalBytes);
            return true;
        }

        private bool TryCalculateRunTransferBytes(
            DesktopRunProgressSnapshot progress,
            out long transferredBytes,
            out long totalBytes)
        {
            transferredBytes = 0;
            totalBytes = 0;
            if (!IsCountedRunStage(progress.Stage) || progress.BytesTotal is not > 0)
            {
                return false;
            }

            totalBytes = progress.BytesTotal.Value;
            _runCompletedTransferBytesByPair.TryGetValue(progress.SyncPairId, out long observedCompletedBytes);
            transferredBytes = Math.Clamp(Math.Max(progress.BytesCompleted, observedCompletedBytes), 0, totalBytes);
            foreach (KeyValuePair<RunTransferProgressKey, long> activeTransfer in _runTransferBytesByKey)
            {
                if (activeTransfer.Key.SyncPairId != progress.SyncPairId)
                {
                    continue;
                }

                transferredBytes += Math.Max(0, activeTransfer.Value);
            }

            transferredBytes = Math.Clamp(transferredBytes, 0, totalBytes);
            return true;
        }

        private double GetDisplayedRunProgressUnits(DesktopRunProgressSnapshot progress)
        {
            if (progress.FilesTotal is not > 0)
            {
                return progress.IsCompleted ? 1 : 0;
            }

            int total = progress.FilesTotal.Value;
            double completed = Math.Clamp(progress.FilesCompleted, 0, total);
            if (!progress.IsCompleted
                && IsCountedRunStage(progress.Stage)
                && _transferProgressByPair.TryGetValue(progress.SyncPairId, out DesktopTransferProgressSnapshot? transfer)
                && transfer.TotalBytes is > 0)
            {
                double transferred = Math.Clamp(transfer.TransferredBytes, 0, transfer.TotalBytes.Value);
                return Math.Clamp(completed + transferred / transfer.TotalBytes.Value, 0, total);
            }

            return GetDisplayedRunProgressCount(progress);
        }

        private static bool IsRunTransferDirection(SyncTransferDirection direction)
        {
            return direction is SyncTransferDirection.Upload or SyncTransferDirection.Download;
        }

        private bool TryGetRunTransferSpeed(out double bytesPerSecond)
        {
            if (_runTransferSpeedBytesPerSecond is > 0)
            {
                bytesPerSecond = _runTransferSpeedBytesPerSecond.Value;
                return true;
            }

            bytesPerSecond = 0;
            return false;
        }

        private bool TryCalculateAggregateTransferProgressValue(out double progressValue)
        {
            progressValue = 0;
            if (_transferProgressByPair.Count == 0)
            {
                return false;
            }

            long transferredBytes = 0;
            long totalBytes = 0;
            foreach (DesktopTransferProgressSnapshot progress in _transferProgressByPair.Values)
            {
                if (progress.TotalBytes is not > 0)
                {
                    return false;
                }

                totalBytes += progress.TotalBytes.Value;
                transferredBytes += Math.Clamp(progress.TransferredBytes, 0, progress.TotalBytes.Value);
            }

            if (totalBytes <= 0)
            {
                return false;
            }

            progressValue = Math.Clamp((double)transferredBytes / totalBytes * 100, 0, 100);
            return true;
        }

        private static string CreateAggregateRunProgressDetails(IReadOnlyList<DesktopRunProgressSnapshot> progressValues)
        {
            int completedFiles = 0;
            int totalFiles = 0;
            bool hasUnknownTotals = false;
            foreach (DesktopRunProgressSnapshot progress in progressValues)
            {
                if (!progress.FilesTotal.HasValue)
                {
                    hasUnknownTotals = true;
                    completedFiles += GetDisplayedRunProgressCount(progress);
                    continue;
                }

                completedFiles += GetDisplayedRunProgressCount(progress);
                totalFiles += progress.FilesTotal.Value;
            }

            if (hasUnknownTotals)
            {
                if (completedFiles > 0 && progressValues.All(static progress => progress.Stage == SyncRunProgressStage.ScanningLocal))
                {
                    return completedFiles.ToString(CultureInfo.CurrentCulture)
                        + (completedFiles == 1 ? " file found across " : " files found across ")
                        + progressValues.Count.ToString(CultureInfo.CurrentCulture)
                        + " folders";
                }

                if (completedFiles > 0 && progressValues.All(static progress => progress.Stage == SyncRunProgressStage.ScanningRemote))
                {
                    return completedFiles.ToString(CultureInfo.CurrentCulture)
                        + (completedFiles == 1 ? " cloud file found across " : " cloud files found across ")
                        + progressValues.Count.ToString(CultureInfo.CurrentCulture)
                        + " folders";
                }

                return progressValues.Count.ToString(CultureInfo.CurrentCulture) + " folders are syncing.";
            }

            return completedFiles.ToString(CultureInfo.CurrentCulture)
                + " of "
                + totalFiles.ToString(CultureInfo.CurrentCulture)
                + " files across "
                + progressValues.Count.ToString(CultureInfo.CurrentCulture)
                + " folders";
        }

        private static string CreateSingleRunProgressDetails(DesktopRunProgressSnapshot progress)
        {
            string label = GetRunStageLabel(progress.Stage);
            string details = CreateRunProgressDetails(progress);
            if (string.IsNullOrWhiteSpace(details))
            {
                return label;
            }

            if (!progress.FilesTotal.HasValue && progress.FilesCompleted <= 0)
            {
                return details;
            }

            return label + " · " + details;
        }

        private static string CreateRunProgressOperation(DesktopRunProgressSnapshot progress)
        {
            string label = GetRunStageLabel(progress.Stage);
            if (progress.FilesTotal.HasValue && IsCountedRunStage(progress.Stage))
            {
                return label + " " + GetDisplayedRunProgressCount(progress).ToString(CultureInfo.CurrentCulture)
                    + " of " + progress.FilesTotal.Value.ToString(CultureInfo.CurrentCulture);
            }

            return label;
        }

        private static string CreateRunProgressDetails(DesktopRunProgressSnapshot progress)
        {
            if (progress.FilesTotal.HasValue)
            {
                int displayCount = GetDisplayedRunProgressCount(progress);
                string unitName = GetRunProgressUnitName(progress.Stage, displayCount, progress.FilesTotal.Value);
                string details = displayCount.ToString(CultureInfo.CurrentCulture)
                    + " of "
                    + progress.FilesTotal.Value.ToString(CultureInfo.CurrentCulture)
                    + " "
                    + unitName;
                return IsCountedRunStage(progress.Stage) || string.IsNullOrWhiteSpace(progress.CurrentPath)
                    ? details
                    : details + " · " + GetDisplayFileName(progress.CurrentPath);
            }

            return progress.Stage switch
            {
                SyncRunProgressStage.ScanningLocal => CreateLocalScanProgressDetails(progress),
                SyncRunProgressStage.ScanningRemote => CreateRemoteScanProgressDetails(progress),
                SyncRunProgressStage.ReconcilingDirectories => "Preparing folders.",
                SyncRunProgressStage.Completed => "Sync pass completed.",
                _ => "Preparing sync.",
            };
        }

        private static string CreateLocalScanProgressDetails(DesktopRunProgressSnapshot progress)
        {
            if (progress.FilesCompleted <= 0)
            {
                if (!string.IsNullOrWhiteSpace(progress.CurrentPath))
                {
                    return "Looking for local changes · " + GetDisplayFileName(progress.CurrentPath);
                }

                return "Looking for local changes.";
            }

            string details = progress.FilesCompleted.ToString(CultureInfo.CurrentCulture)
                + (progress.FilesCompleted == 1 ? " file found" : " files found");
            if (!string.IsNullOrWhiteSpace(progress.CurrentPath))
            {
                details += " · " + GetDisplayFileName(progress.CurrentPath);
            }

            return details;
        }

        private static bool IsCountedRunStage(SyncRunProgressStage stage)
        {
            return stage == SyncRunProgressStage.ReconcilingDirectories
                || stage == SyncRunProgressStage.ReconcilingFiles;
        }

        private static int GetDisplayedRunProgressCount(DesktopRunProgressSnapshot progress)
        {
            if (!progress.IsCompleted
                && IsCountedRunStage(progress.Stage)
                && progress.FilesTotal is > 0
                && progress.FilesCompleted == 0
                && !string.IsNullOrWhiteSpace(progress.CurrentPath))
            {
                return 1;
            }

            return progress.FilesCompleted;
        }

        private static string GetRunProgressUnitName(SyncRunProgressStage stage, int completed, int total)
        {
            bool singular = completed == 1 && total == 1;
            if (stage == SyncRunProgressStage.ReconcilingDirectories)
            {
                return singular ? "folder" : "folders";
            }

            return singular ? "file" : "files";
        }

        private static string CreateRemoteScanProgressDetails(DesktopRunProgressSnapshot progress)
        {
            if (progress.FilesCompleted <= 0)
            {
                if (!string.IsNullOrWhiteSpace(progress.CurrentPath))
                {
                    return "Checking Cotton Cloud · " + GetDisplayFileName(progress.CurrentPath);
                }

                return "Checking Cotton Cloud.";
            }

            string details = progress.FilesCompleted.ToString(CultureInfo.CurrentCulture)
                + (progress.FilesCompleted == 1 ? " cloud file found" : " cloud files found");
            if (!string.IsNullOrWhiteSpace(progress.CurrentPath))
            {
                details += " · " + GetDisplayFileName(progress.CurrentPath);
            }

            return details;
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
            string action = CreateTransferAction(progress.Direction, progress.IsCompleted);
            return syncPairName + ": " + action + " " + GetDisplayFileName(progress.RelativePath);
        }

        private static string CreateTransferOperation(DesktopTransferProgressSnapshot progress)
        {
            string action = CreateTransferAction(progress.Direction, isCompleted: false);
            return action + " " + GetDisplayFileName(progress.RelativePath);
        }

        private static string CreateTransferAction(SyncTransferDirection direction, bool isCompleted)
        {
            return direction switch
            {
                SyncTransferDirection.Upload => isCompleted ? "Uploaded" : "Uploading",
                SyncTransferDirection.Download => isCompleted ? "Downloaded" : "Downloading",
                SyncTransferDirection.Hash => isCompleted ? "Checked" : "Checking",
                _ => isCompleted ? "Synced" : "Syncing",
            };
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

        private static string CreateAggregateTransferDetails(
            IEnumerable<DesktopTransferProgressSnapshot> progressValues,
            bool includeEstimatedTimeRemaining)
        {
            TransferMetricDetails details = CreateAggregateTransferMetricDetails(
                progressValues,
                includeEstimatedTimeRemaining);
            return string.IsNullOrWhiteSpace(details.Rate) ? details.Size : details.Size + " · " + details.Rate;
        }

        private static string CreateHeaderDetails(string size, string rate)
        {
            if (string.IsNullOrWhiteSpace(size))
            {
                return rate;
            }

            return string.IsNullOrWhiteSpace(rate) ? size : size + " · " + rate;
        }

        private static TransferMetricDetails CreateAggregateTransferMetricDetails(
            IEnumerable<DesktopTransferProgressSnapshot> progressValues,
            bool includeEstimatedTimeRemaining = true)
        {
            long transferredBytes = 0;
            long totalBytes = 0;
            bool hasTotalBytes = true;
            double speedBytesPerSecond = 0;
            TimeSpan? longestEstimatedTimeRemaining = null;
            foreach (DesktopTransferProgressSnapshot progress in progressValues)
            {
                transferredBytes += progress.TransferredBytes;
                if (progress.TotalBytes.HasValue)
                {
                    totalBytes += progress.TotalBytes.Value;
                }
                else
                {
                    hasTotalBytes = false;
                }

                if (progress.SpeedBytesPerSecond is > 0)
                {
                    speedBytesPerSecond += progress.SpeedBytesPerSecond.Value;
                    if (progress.TotalBytes is > 0 && progress.TotalBytes.Value > progress.TransferredBytes)
                    {
                        TimeSpan transferRemaining = progress.EstimatedTimeRemaining
                            ?? TimeSpan.FromSeconds((progress.TotalBytes.Value - progress.TransferredBytes) / progress.SpeedBytesPerSecond.Value);
                        if (!longestEstimatedTimeRemaining.HasValue || transferRemaining > longestEstimatedTimeRemaining.Value)
                        {
                            longestEstimatedTimeRemaining = transferRemaining;
                        }
                    }
                }
            }

            string details = hasTotalBytes
                ? FormatBytes(transferredBytes) + " / " + FormatBytes(totalBytes)
                : FormatBytes(transferredBytes);
            if (speedBytesPerSecond <= 0)
            {
                return new TransferMetricDetails(details, string.Empty);
            }

            string rate = FormatBytes(speedBytesPerSecond) + "/s";
            if (includeEstimatedTimeRemaining && longestEstimatedTimeRemaining.HasValue)
            {
                rate += " · " + FormatDuration(longestEstimatedTimeRemaining.Value) + " left";
            }

            return new TransferMetricDetails(details, rate);
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

        private static string FormatFileRate(double filesPerSecond)
        {
            double roundedValue = filesPerSecond >= 10
                ? Math.Round(filesPerSecond)
                : Math.Round(filesPerSecond, 1);
            string format = roundedValue >= 10 || Math.Abs(roundedValue - Math.Round(roundedValue)) < 0.05
                ? "0"
                : "0.0";
            string unit = Math.Abs(roundedValue - 1) < 0.05 ? " file/s" : " files/s";
            return roundedValue.ToString(format, CultureInfo.CurrentCulture) + unit;
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 60)
            {
                int seconds = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
                if (seconds >= 10)
                {
                    seconds = RoundUp(seconds, 5);
                }

                return seconds.ToString(CultureInfo.CurrentCulture) + "s";
            }

            if (duration.TotalMinutes < 60)
            {
                int totalSeconds = RoundUp(Math.Max(60, (int)Math.Ceiling(duration.TotalSeconds)), 5);
                return (totalSeconds / 60).ToString(CultureInfo.CurrentCulture)
                    + "m "
                    + (totalSeconds % 60).ToString("00", CultureInfo.CurrentCulture)
                    + "s";
            }

            int totalMinutes = RoundUp(Math.Max(60, (int)Math.Ceiling(duration.TotalMinutes)), 5);
            return (totalMinutes / 60).ToString(CultureInfo.CurrentCulture)
                + "h "
                + (totalMinutes % 60).ToString("00", CultureInfo.CurrentCulture)
                + "m";
        }

        private static int RoundUp(int value, int step)
        {
            return ((value + step - 1) / step) * step;
        }

        private static double CalculateExponentialSmoothingFactor(TimeSpan elapsed, TimeSpan timeConstant)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                return 0;
            }

            return 1 - Math.Exp(-elapsed.TotalSeconds / timeConstant.TotalSeconds);
        }

        private readonly record struct RunTransferProgressKey(
            Guid SyncPairId,
            SyncTransferDirection Direction,
            string RelativePath);

        private readonly record struct RunFileProgressSample(double CompletedFiles, int TotalFiles, DateTime OccurredAtUtc);

        private readonly record struct RunTransferProgressSample(long TransferredBytes, DateTime OccurredAtUtc);

        private readonly record struct TransferMetricDetails(string Size, string Rate);

        private static bool IsActiveProgressPair(SyncPairRowViewModel syncPair)
        {
            return !string.IsNullOrWhiteSpace(syncPair.CurrentOperation)
                || string.Equals(syncPair.Status, "Scanning", StringComparison.Ordinal)
                || string.Equals(syncPair.Status, "Syncing", StringComparison.Ordinal)
                || string.Equals(syncPair.Status, "Sync requested", StringComparison.Ordinal)
                || string.Equals(syncPair.Status, "Pausing", StringComparison.Ordinal);
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
}
