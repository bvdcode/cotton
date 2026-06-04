// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Main desktop synchronization shell window.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double DashboardHeight = 540;
    private const double DashboardMinHeight = 520;
    private const double DashboardMinWidth = 388;
    private const double DashboardWidth = 400;
    private const double SetupServerHeight = 300;
    private const double SetupServerMinHeight = 280;
    private const double SetupSignInHeight = 360;
    private const double SetupSignInMinHeight = 340;
    private const double SetupMinWidth = 316;
    private const double SetupWidth = 336;

    private readonly DesktopWindowLifecyclePolicy _lifecyclePolicy;
    private WindowProfile? _windowProfile;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow" /> class.
    /// </summary>
    public MainWindow()
        : this(DesktopShellController.CreateDefault(), false, false)
    {
    }

    internal MainWindow(
        IDesktopShellController controller,
        bool hideAfterSessionRestore = false,
        bool canHideToTray = false)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _lifecyclePolicy = new DesktopWindowLifecyclePolicy(hideAfterSessionRestore, canHideToTray);
        InitializeComponent();
        var viewModel = new ShellViewModel(
            controller,
            new WindowLocalFolderPicker(this),
            DesktopNotificationServiceFactory.CreateDefault(),
            new AvaloniaDesktopThemeService());
        DataContext = viewModel;
        ApplyWindowMode(viewModel);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Opened += async (_, _) =>
        {
            await viewModel.InitializeAsync().ConfigureAwait(true);
            if (_lifecyclePolicy.ShouldHideAfterSessionRestore(viewModel.IsDashboardVisible))
            {
                Hide();
            }
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            Closing -= OnClosing;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Dispose();
        };
    }

    internal void RequestQuit()
    {
        _lifecyclePolicy.RequestQuit();
    }

    internal void ShowShell()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_lifecyclePolicy.ResolveCloseAction() == DesktopWindowCloseAction.Close)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsDashboardVisible) && sender is ShellViewModel viewModel)
        {
            ApplyWindowMode(viewModel);
            return;
        }

        if (e.PropertyName == nameof(ShellViewModel.IsSignInStepVisible) && sender is ShellViewModel setupViewModel)
        {
            ApplyWindowMode(setupViewModel);
        }
    }

    private void RemoteFoldersListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ShellViewModel viewModel
            || !viewModel.OpenRemoteFolderCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenRemoteFolderCommand.Execute(null);
    }

    private void ApplyWindowMode(ShellViewModel viewModel)
    {
        WindowProfile profile = ResolveWindowProfile(viewModel);
        if (_windowProfile == profile)
        {
            return;
        }

        _windowProfile = profile;
        MinWidth = profile == WindowProfile.Dashboard ? DashboardMinWidth : SetupMinWidth;
        MinHeight = profile switch
        {
            WindowProfile.Dashboard => DashboardMinHeight,
            WindowProfile.SetupSignIn => SetupSignInMinHeight,
            _ => SetupServerMinHeight,
        };
        Width = profile == WindowProfile.Dashboard ? DashboardWidth : SetupWidth;
        Height = profile switch
        {
            WindowProfile.Dashboard => DashboardHeight,
            WindowProfile.SetupSignIn => SetupSignInHeight,
            _ => SetupServerHeight,
        };
        CenterOnCurrentScreen();
    }

    private static WindowProfile ResolveWindowProfile(ShellViewModel viewModel)
    {
        if (viewModel.IsDashboardVisible)
        {
            return WindowProfile.Dashboard;
        }

        return viewModel.IsSignInStepVisible ? WindowProfile.SetupSignIn : WindowProfile.SetupServer;
    }

    private void CenterOnCurrentScreen()
    {
        Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        double scale = screen.Scaling;
        int pixelWidth = (int)Math.Round(Width * scale);
        int pixelHeight = (int)Math.Round(Height * scale);
        PixelRect workingArea = screen.WorkingArea;
        Position = new PixelPoint(
            workingArea.X + Math.Max(0, workingArea.Width - pixelWidth) / 2,
            workingArea.Y + Math.Max(0, workingArea.Height - pixelHeight) / 2);
    }

    private enum WindowProfile
    {
        SetupServer,
        SetupSignIn,
        Dashboard,
    }
}
