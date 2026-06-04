// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopTrayController : IDisposable
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    private readonly MainWindow _window;
    private readonly TrayIcon _trayIcon;
    private NativeMenuItem? _openFolderMenuItem;
    private NativeMenuItem? _openWebMenuItem;
    private NativeMenuItem? _pauseResumeMenuItem;
    private NativeMenuItem? _settingsMenuItem;
    private NativeMenuItem? _syncNowMenuItem;
    private Uri _currentIconUri = DesktopTrayIconAssetResolver.Resolve(DesktopTrayStatusKind.SignedOut);
    private ShellViewModel? _viewModel;
    private bool _disposed;

    public DesktopTrayController(MainWindow window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        _trayIcon = CreateTrayIcon();
        AttachViewModel(_window.DataContext as ShellViewModel);
    }

    public static bool IsSupportedPlatform => DesktopPlatformCapabilities.IsTrayLifecycleSupported;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        AttachViewModel(null);
        _trayIcon.Dispose();
        _disposed = true;
    }

    private static WindowIcon LoadIcon(Uri iconUri)
    {
        using Stream stream = AssetLoader.Open(iconUri);
        return new WindowIcon(stream);
    }

    private static NativeMenuItem CreateMenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private TrayIcon CreateTrayIcon()
    {
        _openFolderMenuItem = CreateMenuItem("Open selected folder", () => Execute(commandSource => commandSource.OpenFolderCommand));
        _openWebMenuItem = CreateMenuItem("Open Cotton Cloud", () => Execute(commandSource => commandSource.OpenWebCommand));
        _syncNowMenuItem = CreateMenuItem("Sync now", () => Execute(commandSource => commandSource.SyncNowCommand));
        _pauseResumeMenuItem = CreateMenuItem("Pause", () => Execute(commandSource => commandSource.PauseResumeCommand));
        _settingsMenuItem = CreateMenuItem("Settings", ShowSettings);
        var trayIcon = new TrayIcon
        {
            Icon = LoadIcon(_currentIconUri),
            ToolTipText = "Cotton Sync",
            IsVisible = true,
            Menu = new NativeMenu
            {
                Items =
                {
                    CreateMenuItem("Show", ShowWindow),
                    _openFolderMenuItem,
                    _openWebMenuItem,
                    _syncNowMenuItem,
                    _pauseResumeMenuItem,
                    _settingsMenuItem,
                    new NativeMenuItemSeparator(),
                    CreateMenuItem("Quit", Quit),
                },
            },
        };
        trayIcon.Clicked += (_, _) => ShowWindow();
        return trayIcon;
    }

    private void AttachViewModel(ShellViewModel? viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateTrayStatus();
        UpdateTrayActions();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.HeaderStatusText)
            or nameof(ShellViewModel.IsSignedIn)
            or nameof(ShellViewModel.HasStatusAttention))
        {
            UpdateTrayStatus();
        }

        if (e.PropertyName is nameof(ShellViewModel.PauseResumeTrayLabel)
            or nameof(ShellViewModel.CanSyncNow)
            or nameof(ShellViewModel.CanTogglePauseResumeSync))
        {
            UpdateTrayActions();
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.IsSignedIn)
            or nameof(ShellViewModel.IsBusy)
            or nameof(ShellViewModel.SelectedSyncPair))
        {
            UpdateTrayActions();
        }
    }

    private void UpdateTrayStatus()
    {
        if (_viewModel is null)
        {
            _trayIcon.ToolTipText = "Cotton Sync";
            return;
        }

        DesktopTrayStatus status = DesktopTrayStatusResolver.FromShellState(
            _viewModel.IsSignedIn,
            _viewModel.HeaderStatusText,
            _viewModel.HasStatusAttention);
        _trayIcon.ToolTipText = status.ToolTipText;
        if (_currentIconUri != status.IconUri)
        {
            _trayIcon.Icon = LoadIcon(status.IconUri);
            _currentIconUri = status.IconUri;
        }
    }

    private void UpdateTrayActions()
    {
        if (_viewModel is null)
        {
            SetMenuItemEnabled(_openFolderMenuItem, false);
            SetMenuItemEnabled(_openWebMenuItem, false);
            SetMenuItemEnabled(_syncNowMenuItem, false);
            SetMenuItemEnabled(_pauseResumeMenuItem, false);
            SetMenuItemEnabled(_settingsMenuItem, false);
            return;
        }

        SetMenuItemEnabled(_openFolderMenuItem, _viewModel.OpenFolderCommand.CanExecute(null));
        SetMenuItemEnabled(_openWebMenuItem, _viewModel.OpenWebCommand.CanExecute(null));
        SetMenuItemEnabled(_syncNowMenuItem, _viewModel.SyncNowCommand.CanExecute(null));
        SetMenuItemEnabled(_settingsMenuItem, _viewModel.ShowSettingsCommand.CanExecute(null));
        if (_pauseResumeMenuItem is not null)
        {
            _pauseResumeMenuItem.Header = _viewModel.PauseResumeTrayLabel;
            _pauseResumeMenuItem.IsEnabled = _viewModel.PauseResumeCommand.CanExecute(null);
        }
    }

    private static void SetMenuItemEnabled(NativeMenuItem? menuItem, bool isEnabled)
    {
        if (menuItem is not null)
        {
            menuItem.IsEnabled = isEnabled;
        }
    }

    private void Execute(Func<ShellViewModel, AsyncRelayCommand> selectCommand)
    {
        if (_window.DataContext is not ShellViewModel viewModel)
        {
            return;
        }

        AsyncRelayCommand command = selectCommand(viewModel);
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void Quit()
    {
        _window.RequestQuit();
        _lifetime.Shutdown();
    }

    private void ShowWindow()
    {
        _window.ShowShell();
    }

    private void ShowSettings()
    {
        ShowWindow();
        Execute(commandSource => commandSource.ShowSettingsCommand);
    }
}
