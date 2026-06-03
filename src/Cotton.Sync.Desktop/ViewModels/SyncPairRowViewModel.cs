// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels;

/// <summary>
/// Displays one configured synchronization pair.
/// </summary>
internal sealed class SyncPairRowViewModel : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _localPath = string.Empty;
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
