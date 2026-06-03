// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
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
    private const double SetupHeight = 382;
    private const double SetupMinHeight = 360;
    private const double SetupMinWidth = 324;
    private const double SetupWidth = 344;

    private readonly DesktopWindowLifecyclePolicy _lifecyclePolicy;
    private bool? _isDashboardWindowMode;

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
        ApplyWindowMode(viewModel.IsDashboardVisible);
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
            ApplyWindowMode(viewModel.IsDashboardVisible);
        }
    }

    private void ApplyWindowMode(bool isDashboard)
    {
        if (_isDashboardWindowMode == isDashboard)
        {
            return;
        }

        _isDashboardWindowMode = isDashboard;
        MinWidth = isDashboard ? DashboardMinWidth : SetupMinWidth;
        MinHeight = isDashboard ? DashboardMinHeight : SetupMinHeight;
        Width = isDashboard ? DashboardWidth : SetupWidth;
        Height = isDashboard ? DashboardHeight : SetupHeight;
        CenterOnCurrentScreen();
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
}
