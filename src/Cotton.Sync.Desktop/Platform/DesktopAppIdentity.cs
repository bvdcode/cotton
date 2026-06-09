// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cotton.Sync.Desktop.Platform
{

    internal static class DesktopAppIdentity
    {
        public const string AppUserModelId = "Cotton.Sync.Desktop";

        public static void ApplyToCurrentProcess()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                int result = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
                if (result != 0)
                {
                    Trace.TraceWarning("Failed to set Cotton Sync AppUserModelID. HRESULT: 0x{0:X8}", result);
                }
            }
            catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
            {
                Trace.TraceWarning("Failed to set Cotton Sync AppUserModelID: {0}", exception);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    }
}
