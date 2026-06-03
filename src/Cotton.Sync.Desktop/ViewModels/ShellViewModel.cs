// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
    private string _accountName = "Signed out";
    private string _globalStatus = "Loading";
    private bool _isBusy;
    private bool _isSignedIn;
    private string _localFolderPath = string.Empty;
    private string _password = string.Empty;
    private string _remoteFolderPath = string.Empty;
    private string _serverUrl = string.Empty;
    private SyncPairRowViewModel? _selectedSyncPair;
    private string _totpCode = string.Empty;
    private bool _trustDevice = true;
    private string _username = string.Empty;

    internal ShellViewModel(IDesktopShellController controller, ILocalFolderPicker folderPicker)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        SignInCommand = new AsyncRelayCommand(SignInAsync, CanSignIn, HandleCommandError);
        AddSyncPairCommand = new AsyncRelayCommand(AddSyncPairAsync, CanAddSyncPair, HandleCommandError);
        BrowseLocalFolderCommand = new AsyncRelayCommand(BrowseLocalFolderAsync, () => !IsBusy, HandleCommandError);
        SyncNowCommand = new AsyncRelayCommand(SyncNowAsync, () => IsSignedIn, HandleCommandError);
        PauseCommand = new AsyncRelayCommand(PauseAsync, () => IsSignedIn, HandleCommandError);
        ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => IsSignedIn, HandleCommandError);
        SignOutCommand = new AsyncRelayCommand(SignOutAsync, () => IsSignedIn, HandleCommandError);
        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, () => SelectedSyncPair is not null, HandleCommandError);
    }

    public ObservableCollection<SyncPairRowViewModel> SyncPairs { get; } = [];

    public ObservableCollection<ActivityRowViewModel> Activities { get; } = [];

    public AsyncRelayCommand AddSyncPairCommand { get; }

    public AsyncRelayCommand BrowseLocalFolderCommand { get; }

    public AsyncRelayCommand OpenFolderCommand { get; }

    public AsyncRelayCommand PauseCommand { get; }

    public AsyncRelayCommand ResumeCommand { get; }

    public AsyncRelayCommand SignInCommand { get; }

    public AsyncRelayCommand SignOutCommand { get; }

    public AsyncRelayCommand SyncNowCommand { get; }

    public string AccountName
    {
        get => _accountName;
        private set => SetProperty(ref _accountName, value);
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
                RaiseCommandStates();
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
                SignInCommand.RaiseCanExecuteChanged();
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

    public bool TrustDevice
    {
        get => _trustDevice;
        set => SetProperty(ref _trustDevice, value);
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
        _controller.Dispose();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            DesktopShellSnapshot snapshot = await _controller.LoadAsync().ConfigureAwait(true);
            ServerUrl = snapshot.ServerUrl.AbsoluteUri;
            SyncPairs.Clear();
            foreach (DesktopSyncPairSnapshot syncPair in snapshot.SyncPairs)
            {
                SyncPairs.Add(ToRow(syncPair));
            }

            SelectedSyncPair = SyncPairs.FirstOrDefault();
            GlobalStatus = SyncPairs.Count == 0 ? "Ready to connect" : "Ready";
            AddActivity("App", string.Empty, "Settings loaded");
            RaiseCommandStates();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleCommandError(exception);
        }
        finally
        {
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
            GlobalStatus = "Sync requested";
            AddActivity("Pair", syncPair.LocalRootPath, "Folder added and initial sync requested");
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

    private async Task PauseAsync()
    {
        IsBusy = true;
        try
        {
            await _controller.PauseAllAsync().ConfigureAwait(true);
            GlobalStatus = "Paused";
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
                new DesktopSignInRequest(ServerUrl, Username, Password, TotpCode, TrustDevice)).ConfigureAwait(true);
            IsSignedIn = true;
            AccountName = session.Email ?? session.Username;
            Password = string.Empty;
            GlobalStatus = "Connected";
            AddActivity("Account", AccountName, "Signed in");
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
            SetAllPairStatuses("Idle");
            AddActivity("Account", string.Empty, "Signed out");
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
            SetAllPairStatuses("Sync requested");
            AddActivity("Sync", string.Empty, "Manual sync requested");
        }
        finally
        {
            IsBusy = false;
        }
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
            && !string.IsNullOrEmpty(Password);
    }

    private void HandleCommandError(Exception exception)
    {
        Trace.TraceError(exception.ToString());
        GlobalStatus = "Action failed";
        AddActivity("Error", string.Empty, exception.Message);
        IsBusy = false;
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
        SyncNowCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
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
            RemotePath = syncPair.RemotePath,
            Status = syncPair.Status,
        };
    }
}
