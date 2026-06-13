// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Cotton.Sync.Desktop.Shell
{
    internal class DesktopWindowLifecyclePolicy
    {
        private readonly bool _canHideToTray;
        private readonly bool _startMinimizedToTray;
        private bool _isQuitRequested;

        public DesktopWindowLifecyclePolicy(bool startMinimizedToTray, bool canHideToTray)
        {
            _canHideToTray = canHideToTray;
            _startMinimizedToTray = startMinimizedToTray && canHideToTray;
        }

        public bool ShouldHideAfterStartup()
        {
            return _startMinimizedToTray;
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
}
