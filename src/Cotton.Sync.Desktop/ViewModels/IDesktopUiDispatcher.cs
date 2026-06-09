// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.ViewModels
{
    internal interface IDesktopUiDispatcher
    {
        bool CheckAccess();

        void Post(Action action);

        Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
    }
}
