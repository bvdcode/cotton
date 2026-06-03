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
    private const double DashboardHeight = 680;
    private const double DashboardMinHeight = 560;
    private const double DashboardMinWidth = 760;
    private const double DashboardWidth = 980;
    private const double SetupHeight = 430;
    private const double SetupMinHeight = 410;
    private const double SetupMinWidth = 400;
    private const double SetupWidth = 420;

    private bool? _isDashboardWindowMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow" /> class.
    /// </summary>
    public MainWindow()
        : this(DesktopShellController.CreateDefault())
    {
    }

    internal MainWindow(IDesktopShellController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        InitializeComponent();
        var viewModel = new ShellViewModel(controller, new WindowLocalFolderPicker(this));
        DataContext = viewModel;
        ApplyWindowMode(viewModel.IsDashboardVisible);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Opened += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Dispose();
        };
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
