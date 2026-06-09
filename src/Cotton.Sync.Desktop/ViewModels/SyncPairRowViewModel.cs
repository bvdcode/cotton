// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.ViewModels
{
    /// <summary>
    /// Displays one configured synchronization pair.
    /// </summary>
    internal class SyncPairRowViewModel : ViewModelBase
    {
        private string _displayName = string.Empty;
        private string _editableDisplayName = string.Empty;
        private long? _changeCursor;
        private string _currentOperation = string.Empty;
        private double _currentProgressValue;
        private bool _hasCurrentProgress;
        private bool _isEnabled = true;
        private bool _isEditorVisible;
        private bool _isCurrentProgressIndeterminate;
        private DateTime? _lastSyncedAtUtc;
        private string? _lastError;
        private string _localPath = string.Empty;
        private SyncPairMode _mode = SyncPairMode.FullMirror;
        private Guid? _remoteRootNodeId;
        private string _remotePath = string.Empty;
        private string _status = string.Empty;

        public Guid Id { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    OnPropertyChanged(nameof(ToggleEnabledLabel));
                    OnPropertyChanged(nameof(IsDisabled));
                    OnPropertyChanged(nameof(IsStatusIndicatorVisible));
                    OnPropertyChanged(nameof(IsStatusActive));
                    OnPropertyChanged(nameof(StatusIndicatorToolTip));
                }
            }
        }

        public bool IsDisabled => !IsEnabled;

        public string ToggleEnabledLabel => IsEnabled ? "Disable sync folder" : "Enable sync folder";

        public bool IsEditorVisible
        {
            get => _isEditorVisible;
            set => SetProperty(ref _isEditorVisible, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string CurrentOperation
        {
            get => _currentOperation;
            set
            {
                if (SetProperty(ref _currentOperation, value))
                {
                    OnPropertyChanged(nameof(HasCurrentOperation));
                }
            }
        }

        public bool HasCurrentOperation => !string.IsNullOrWhiteSpace(CurrentOperation);

        public string DisplayStatus
        {
            get
            {
                string status = Status.Trim();
                return IsDecorativeIdleStatus(status) ? string.Empty : status;
            }
        }

        public bool HasDisplayStatus => !string.IsNullOrWhiteSpace(DisplayStatus);

        public bool IsStatusIndicatorVisible => IsEnabled || HasDisplayStatus;

        public string StatusIndicatorToolTip => HasDisplayStatus ? DisplayStatus : "Sync enabled";

        public bool IsStatusActive =>
            (IsEnabled && !HasDisplayStatus)
            || string.Equals(DisplayStatus, "Scanning", StringComparison.Ordinal)
            || string.Equals(DisplayStatus, "Syncing", StringComparison.Ordinal)
            || string.Equals(DisplayStatus, "Sync requested", StringComparison.Ordinal);

        public bool IsStatusPaused =>
            string.Equals(DisplayStatus, "Paused", StringComparison.Ordinal)
            || string.Equals(DisplayStatus, "Pausing", StringComparison.Ordinal);

        public bool IsStatusAttention =>
            string.Equals(DisplayStatus, "Error", StringComparison.Ordinal)
            || string.Equals(DisplayStatus, "Conflict", StringComparison.Ordinal)
            || string.Equals(DisplayStatus, "Offline", StringComparison.Ordinal);

        public bool HasCurrentProgress
        {
            get => _hasCurrentProgress;
            set => SetProperty(ref _hasCurrentProgress, value);
        }

        public double CurrentProgressValue
        {
            get => _currentProgressValue;
            set => SetProperty(ref _currentProgressValue, value);
        }

        public bool IsCurrentProgressIndeterminate
        {
            get => _isCurrentProgressIndeterminate;
            set => SetProperty(ref _isCurrentProgressIndeterminate, value);
        }

        public string EditableDisplayName
        {
            get => _editableDisplayName;
            set => SetProperty(ref _editableDisplayName, value);
        }

        public string LocalPath
        {
            get => _localPath;
            set => SetProperty(ref _localPath, value);
        }

        public SyncPairMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public DateTime? LastSyncedAtUtc
        {
            get => _lastSyncedAtUtc;
            set => SetProperty(ref _lastSyncedAtUtc, value);
        }

        public long? ChangeCursor
        {
            get => _changeCursor;
            set => SetProperty(ref _changeCursor, value);
        }

        public string? LastError
        {
            get => _lastError;
            set => SetProperty(ref _lastError, value);
        }

        public Guid? RemoteRootNodeId
        {
            get => _remoteRootNodeId;
            set => SetProperty(ref _remoteRootNodeId, value);
        }

        public string RemotePath
        {
            get => _remotePath;
            set => SetProperty(ref _remotePath, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(DisplayStatus));
                    OnPropertyChanged(nameof(HasDisplayStatus));
                    OnPropertyChanged(nameof(IsStatusIndicatorVisible));
                    OnPropertyChanged(nameof(StatusIndicatorToolTip));
                    OnPropertyChanged(nameof(IsStatusActive));
                    OnPropertyChanged(nameof(IsStatusPaused));
                    OnPropertyChanged(nameof(IsStatusAttention));
                }
            }
        }

        private static bool IsDecorativeIdleStatus(string status)
        {
            return string.Equals(status, "Idle", StringComparison.OrdinalIgnoreCase);
        }
    }
}
