// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Displays one configured synchronization pair.
/// </summary>
internal sealed class SyncPairRowViewModel : ViewModelBase
{
    private string _displayName = string.Empty;
    private long? _changeCursor;
    private DateTime? _lastSyncedAtUtc;
    private string? _lastError;
    private string _localPath = string.Empty;
    private Guid? _remoteRootNodeId;
    private string _remotePath = string.Empty;
    private string _status = string.Empty;

    public Guid Id { get; set; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string LocalPath
    {
        get => _localPath;
        set => SetProperty(ref _localPath, value);
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
