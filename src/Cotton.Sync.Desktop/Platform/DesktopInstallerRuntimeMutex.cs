// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.Diagnostics;

namespace Cotton.Sync.Desktop.Platform
{
    internal class DesktopInstallerRuntimeMutex : IDisposable
    {
        public const string MutexName = "CottonSyncDesktop_B671C18E_1E77_437C_AB9B_5C5C9D877E18";

        private readonly Mutex? _mutex;

        private DesktopInstallerRuntimeMutex(Mutex? mutex)
        {
            _mutex = mutex;
        }

        public static DesktopInstallerRuntimeMutex CreateForCurrentPlatform()
        {
            if (!OperatingSystem.IsWindows())
            {
                return new DesktopInstallerRuntimeMutex(null);
            }

            try
            {
                return new DesktopInstallerRuntimeMutex(new Mutex(initiallyOwned: false, MutexName));
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ApplicationException)
            {
                Trace.TraceWarning("Cotton Sync installer mutex could not be created: {0}", exception.Message);
                return new DesktopInstallerRuntimeMutex(null);
            }
        }

        public void Dispose()
        {
            _mutex?.Dispose();
        }
    }
}
