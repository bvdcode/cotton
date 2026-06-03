// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia.Controls;
using Cotton.Sync.Desktop.ViewModels;

namespace Cotton.Sync.Desktop;

/// <summary>
/// Main desktop synchronization shell window.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow" /> class.
    /// </summary>
    public MainWindow()
        : this(ShellViewModel.CreateDefault())
    {
    }

    internal MainWindow(ShellViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        DataContext = viewModel;
        Opened += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
        Closed += (_, _) => viewModel.Dispose();
    }
}
