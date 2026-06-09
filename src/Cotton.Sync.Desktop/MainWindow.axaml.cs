// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cotton.Sync.Desktop.Platform;
using Cotton.Sync.Desktop.Shell;
using Cotton.Sync.Desktop.Startup;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop
{

    /// <summary>
    /// Main desktop synchronization shell window.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const double DashboardHeight = 540;
        private const double DashboardMinHeight = 520;
        private const double DashboardMinWidth = 388;
        private const double DashboardWidth = 400;
        private const double SetupServerHeight = 288;
        private const double SetupServerMinHeight = 280;
        private const double SetupSignInHeight = 452;
        private const double SetupSignInMinHeight = 440;
        private const double SetupMinWidth = 316;
        private const double SetupWidth = 336;

        private readonly DesktopWindowLifecyclePolicy _lifecyclePolicy;
        private readonly DesktopVisualSmokeScenario? _visualSmokeScenario;
        private bool _hasOpened;
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
            bool canHideToTray = false,
            DesktopVisualSmokeScenario? visualSmokeScenario = null)
        {
            ArgumentNullException.ThrowIfNull(controller);
            _lifecyclePolicy = new DesktopWindowLifecyclePolicy(hideAfterSessionRestore, canHideToTray);
            _visualSmokeScenario = visualSmokeScenario;
            InitializeComponent();
            bool notifyOnSessionRestore = !hideAfterSessionRestore && visualSmokeScenario is null;
            var viewModel = new ShellViewModel(
                controller,
                new WindowLocalFolderPicker(this),
                DesktopNotificationServiceFactory.CreateDefault(),
                new AvaloniaDesktopThemeService(),
                notifyOnSessionRestore: notifyOnSessionRestore);
            DataContext = viewModel;
            ApplyWindowMode(viewModel);
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Opened += async (_, _) =>
            {
                _hasOpened = true;
                CenterOnCurrentScreen();
                await viewModel.InitializeAsync().ConfigureAwait(true);
                await viewModel.ApplyVisualSmokeScenarioAsync(_visualSmokeScenario).ConfigureAwait(true);
                if (_lifecyclePolicy.ShouldHideAfterSessionRestore(viewModel.IsDashboardVisible))
                {
                    Hide();
                }
            };
            Closing += OnClosing;
            Closed += async (_, _) =>
            {
                Closing -= OnClosing;
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                await viewModel.DisposeAsync().ConfigureAwait(true);
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
                return;
            }

            if ((e.PropertyName == nameof(ShellViewModel.IsSelectedSyncPairEditorVisible)
                || e.PropertyName == nameof(ShellViewModel.SelectedSyncPair))
                && sender is ShellViewModel syncPairViewModel)
            {
                ScrollSelectedSyncPairIntoView(syncPairViewModel);
            }
        }

        private void ScrollSelectedSyncPairIntoView(ShellViewModel viewModel)
        {
            if (!viewModel.IsSelectedSyncPairEditorVisible || viewModel.SelectedSyncPair is null)
            {
                return;
            }

            Guid syncPairId = viewModel.SelectedSyncPair.Id;
            Dispatcher.UIThread.Post(() =>
            {
                Control? row = SyncPairsScrollViewer
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(control => control.Tag is Guid rowSyncPairId && rowSyncPairId == syncPairId);

                if (row is null)
                {
                    return;
                }

                row.BringIntoView();
                Dispatcher.UIThread.Post(
                    () => BringSyncPairRowBottomIntoView(row),
                    DispatcherPriority.Background);
            });
        }

        private static void BringSyncPairRowBottomIntoView(Control row)
        {
            if (row.Bounds.Width <= 0 || row.Bounds.Height <= 0)
            {
                row.BringIntoView();
                return;
            }

            row.BringIntoView(new Rect(0, row.Bounds.Height - 1, row.Bounds.Width, 1));
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

        private void SignInInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if ((e.Key != Key.Enter && e.Key != Key.Return)
                || DataContext is not ShellViewModel viewModel
                || !viewModel.SignInCommand.CanExecute(null))
            {
                return;
            }

            e.Handled = true;
            viewModel.SignInCommand.Execute(null);
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
            if (_hasOpened)
            {
                CenterOnCurrentScreen();
            }
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
}
