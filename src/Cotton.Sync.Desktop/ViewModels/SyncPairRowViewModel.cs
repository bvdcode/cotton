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
    private bool _isEnabled = true;
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
                OnPropertyChanged(nameof(ToggleEnabledIcon));
            }
        }
    }

    public string ToggleEnabledLabel => IsEnabled ? "Disable" : "Enable";

    public string ToggleEnabledIcon => IsEnabled ? "⏸" : "▶";

    public string ModeLabel => Mode switch
    {
        SyncPairMode.FullMirror => "Full mirror",
        SyncPairMode.VirtualFilesPlaceholder => "Virtual files",
        _ => "Unknown",
    };

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string CurrentOperation
    {
        get => _currentOperation;
        set => SetProperty(ref _currentOperation, value);
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
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(ModeLabel));
            }
        }
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
        set => SetProperty(ref _status, value);
    }
}
