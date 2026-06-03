// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Avalonia.Threading;

namespace Cotton.Sync.Desktop.ViewModels;

internal sealed class AvaloniaDesktopUiDispatcher : IDesktopUiDispatcher
{
    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }

    public async Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await Dispatcher.UIThread.InvokeAsync(
            action,
            DispatcherPriority.Normal,
            cancellationToken);
    }
}
