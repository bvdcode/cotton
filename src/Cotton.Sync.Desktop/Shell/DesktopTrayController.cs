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
    private NativeMenuItem? _pauseResumeMenuItem;
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
        _pauseResumeMenuItem = CreateMenuItem("Pause", () => Execute(commandSource => commandSource.PauseResumeCommand));
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
                    CreateMenuItem("Open folder", () => Execute(commandSource => commandSource.OpenFolderCommand)),
                    CreateMenuItem("Open web", () => Execute(commandSource => commandSource.OpenWebCommand)),
                    CreateMenuItem("Sync now", () => Execute(commandSource => commandSource.SyncNowCommand)),
                    _pauseResumeMenuItem,
                    CreateMenuItem("Settings", ShowSettings),
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
        if (e.PropertyName is nameof(ShellViewModel.GlobalStatus)
            or nameof(ShellViewModel.IsSignedIn)
            or nameof(ShellViewModel.ActionRequiredMessage)
            or nameof(ShellViewModel.HasActionRequired))
        {
            UpdateTrayStatus();
        }

        if (e.PropertyName is nameof(ShellViewModel.PauseResumeTrayLabel)
            or nameof(ShellViewModel.CanTogglePauseResumeSync))
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
            _viewModel.GlobalStatus,
            _viewModel.HasActionRequired);
        _trayIcon.ToolTipText = status.ToolTipText;
        if (_currentIconUri != status.IconUri)
        {
            _trayIcon.Icon = LoadIcon(status.IconUri);
            _currentIconUri = status.IconUri;
        }
    }

    private void UpdateTrayActions()
    {
        if (_pauseResumeMenuItem is null)
        {
            return;
        }

        _pauseResumeMenuItem.Header = _viewModel?.PauseResumeTrayLabel ?? "Pause";
        _pauseResumeMenuItem.IsEnabled = _viewModel?.CanTogglePauseResumeSync ?? false;
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
