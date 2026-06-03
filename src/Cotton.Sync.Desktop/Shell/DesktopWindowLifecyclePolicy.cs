// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell;

internal sealed class DesktopWindowLifecyclePolicy
{
    private readonly bool _canHideToTray;
    private readonly bool _hideAfterSessionRestore;
    private bool _isQuitRequested;

    public DesktopWindowLifecyclePolicy(bool hideAfterSessionRestore, bool canHideToTray)
    {
        _canHideToTray = canHideToTray;
        _hideAfterSessionRestore = hideAfterSessionRestore && canHideToTray;
    }

    public bool ShouldHideAfterSessionRestore(bool isDashboardVisible)
    {
        return _hideAfterSessionRestore && isDashboardVisible;
    }

    public DesktopWindowCloseAction ResolveCloseAction()
    {
        return _isQuitRequested || !_canHideToTray
            ? DesktopWindowCloseAction.Close
            : DesktopWindowCloseAction.HideToTray;
    }

    public void RequestQuit()
    {
        _isQuitRequested = true;
    }
}
