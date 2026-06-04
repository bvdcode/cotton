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
        _openFolderMenuItem = CreateMenuItem("Open folder", () => Execute(commandSource => commandSource.OpenTrayFolderCommand));
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
            or nameof(ShellViewModel.CanTogglePauseResumeSync)
            or nameof(ShellViewModel.CanOpenTrayFolder)
            or nameof(ShellViewModel.TrayOpenFolderLabel))
        {
            UpdateTrayActions();
            return;
        }

        if (e.PropertyName is nameof(ShellViewModel.IsSignedIn)
            or nameof(ShellViewModel.IsBusy))
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
            SetMenuItemAvailability(_openFolderMenuItem, false);
            SetMenuItemAvailability(_openWebMenuItem, false);
            SetMenuItemAvailability(_syncNowMenuItem, false);
            SetMenuItemAvailability(_pauseResumeMenuItem, false);
            SetMenuItemAvailability(_settingsMenuItem, false);
            return;
        }

        if (_openFolderMenuItem is not null)
        {
            _openFolderMenuItem.Header = _viewModel.TrayOpenFolderLabel;
            SetMenuItemAvailability(
                _openFolderMenuItem,
                _viewModel.CanOpenTrayFolder && _viewModel.OpenTrayFolderCommand.CanExecute(null));
        }

        SetMenuItemAvailability(_openWebMenuItem, _viewModel.OpenWebCommand.CanExecute(null));
        SetMenuItemAvailability(_syncNowMenuItem, _viewModel.SyncNowCommand.CanExecute(null));
        SetMenuItemAvailability(_settingsMenuItem, _viewModel.ShowSettingsCommand.CanExecute(null));
        if (_pauseResumeMenuItem is not null)
        {
            _pauseResumeMenuItem.Header = _viewModel.PauseResumeTrayLabel;
            SetMenuItemAvailability(_pauseResumeMenuItem, _viewModel.PauseResumeCommand.CanExecute(null));
        }
    }

    private static void SetMenuItemAvailability(NativeMenuItem? menuItem, bool isAvailable)
    {
        if (menuItem is not null)
        {
            menuItem.IsVisible = isAvailable;
            menuItem.IsEnabled = isAvailable;
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
