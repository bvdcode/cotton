// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Cotton.Sync.App.SyncPairs;

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Displays one configured synchronization pair.
/// </summary>
internal sealed class SyncPairRowViewModel : ViewModelBase
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
            }
        }
    }

    public bool IsDisabled => !IsEnabled;

    public string ToggleEnabledLabel => IsEnabled ? "Disable sync folder" : "Enable sync folder";

    public bool IsEditorVisible
    {
        get => _isEditorVisible;
        set
        {
            if (SetProperty(ref _isEditorVisible, value))
            {
                OnPropertyChanged(nameof(IsHeaderStatusVisible));
            }
        }
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

    public bool IsHeaderStatusVisible => HasDisplayStatus && !IsEditorVisible;

    public bool IsErrorStatus => string.Equals(DisplayStatus, "Error", StringComparison.Ordinal);

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
                OnPropertyChanged(nameof(IsHeaderStatusVisible));
                OnPropertyChanged(nameof(IsErrorStatus));
            }
        }
    }

    private static bool IsDecorativeIdleStatus(string status)
    {
        return string.Equals(status, "Idle", StringComparison.OrdinalIgnoreCase);
    }
}
