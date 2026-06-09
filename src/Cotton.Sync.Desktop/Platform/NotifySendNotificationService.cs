// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using System.ComponentModel;
using System.Diagnostics;

namespace Cotton.Sync.Desktop.Platform
{

    internal sealed class NotifySendNotificationService : IDesktopNotificationService
    {
        private readonly string _executablePath;
        private readonly string? _iconPath;

        public NotifySendNotificationService(string executablePath, string? iconPath = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            _executablePath = executablePath.Trim();
            _iconPath = string.IsNullOrWhiteSpace(iconPath) ? null : iconPath.Trim();
        }

        public bool IsSupported => true;

        public void Show(string title, string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            try
            {
                Process? process = Process.Start(CreateStartInfo(_executablePath, title, message, _iconPath));
                process?.Dispose();
            }
            catch (Exception exception) when (IsExpectedNotificationFailure(exception))
            {
                Trace.TraceWarning("Failed to show desktop notification: {0}", exception);
            }
        }

        internal static ProcessStartInfo CreateStartInfo(string executablePath, string title, string message)
        {
            return CreateStartInfo(executablePath, title, message, iconPath: null);
        }

        internal static ProcessStartInfo CreateStartInfo(
            string executablePath,
            string title,
            string message,
            string? iconPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("--app-name");
            startInfo.ArgumentList.Add(DesktopNotificationIdentity.AppName);
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                startInfo.ArgumentList.Add("--icon");
                startInfo.ArgumentList.Add(iconPath.Trim());
            }

            startInfo.ArgumentList.Add(title);
            startInfo.ArgumentList.Add(message);
            return startInfo;
        }

        private static bool IsExpectedNotificationFailure(Exception exception)
        {
            return exception is Win32Exception
                or FileNotFoundException
                or InvalidOperationException
                or ObjectDisposedException;
        }
    }
}
